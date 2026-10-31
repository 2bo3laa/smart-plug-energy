using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int ResetCodeTtlMinutes = 15;

    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _logger;
    private readonly GoogleAuthService _google;
    private readonly bool _exposeResetCode;

    public AuthController(AppDbContext db, TokenService tokens, ILogger<AuthController> logger, GoogleAuthService google,
        IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
        _google = google;
        // The reset code is only returned to the client in Development or when Auth:ExposeResetCode=true
        // (demo only: there is no email provider, and returning it lets anyone reset any account).
        _exposeResetCode = env.IsDevelopment() || string.Equals(config["Auth:ExposeResetCode"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static PublicUserDto ToPublic(User u) => new(u.Id, u.Name, u.Email);

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(SignupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { error = "name, email and password are required" });
        }
        if (req.Password.Length < 6)
        {
            return BadRequest(new { error = "Password must be at least 6 characters" });
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { error = "An account with this email already exists" });
        }

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 10)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Demo devices/schedules/alerts so a brand-new account isn't empty.
        SeedData.SeedForUser(_db, user.Id);

        var token = _tokens.CreateToken(user.Id);
        return StatusCode(201, new AuthResponse(token, ToPublic(user)));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { error = "email and password are required" });
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var token = _tokens.CreateToken(user.Id);
        return Ok(new AuthResponse(token, ToPublic(user)));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
        {
            return BadRequest(new { error = "currentPassword and newPassword are required" });
        }
        if (req.NewPassword.Length < 6)
        {
            return BadRequest(new { error = "New password must be at least 6 characters" });
        }

        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { error = "Current password is incorrect" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 10);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
        {
            return BadRequest(new { error = "email is required" });
        }

        // Always respond the same way so emails can't be enumerated.
        object genericResponse() => new { message = "If an account with this email exists, a reset code has been sent." };

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return Ok(genericResponse());

        var code = Random.Shared.Next(100000, 999999).ToString();
        var reset = new PasswordReset
        {
            UserId = user.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code, workFactor: 10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(ResetCodeTtlMinutes)
        };
        _db.PasswordResets.Add(reset);
        await _db.SaveChangesAsync();

        // NOTE: no email/SMS provider is wired up yet - the code is only logged here.
        // Hook up a real provider (SendGrid, Twilio, ...) before shipping this to real
        // users, and drop `devCode` from the response below once you do.
        const string msg = "If an account with this email exists, a reset code has been sent.";
        if (!_exposeResetCode) return Ok(new { message = msg });

        _logger.LogInformation("[password reset] code for {Email}: {Code} (expires in {Ttl}m)", user.Email, code, ResetCodeTtlMinutes);
        return Ok(new { message = msg, devCode = code });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.NewPassword))
        {
            return BadRequest(new { error = "email, code and newPassword are required" });
        }
        if (req.NewPassword.Length < 6)
        {
            return BadRequest(new { error = "New password must be at least 6 characters" });
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return BadRequest(new { error = "Invalid or expired code" });

        var reset = await _db.PasswordResets
            .Where(r => r.UserId == user.Id && !r.Used && r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (reset == null || !BCrypt.Net.BCrypt.Verify(req.Code, reset.CodeHash))
        {
            return BadRequest(new { error = "Invalid or expired code" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 10);
        reset.Used = true;
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // ---- Google account ----

    // POST /api/auth/google-link  { idToken }  (link the signed-in account to a Google account)
    [Authorize]
    [HttpPost("google-link")]
    public async Task<IActionResult> LinkGoogle(GoogleTokenRequest req)
    {
        if (!_google.IsConfigured) return StatusCode(503, new { error = "Google sign-in is not configured on the server" });
        var identity = await _google.VerifyAsync(req.IdToken);
        if (identity == null) return BadRequest(new { error = "Could not verify the Google account" });

        var userId = User.GetUserId();
        if (await _db.Users.AnyAsync(u => u.GoogleId == identity.Sub && u.Id != userId))
        {
            return Conflict(new { error = "This Google account is already linked to another user" });
        }
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });

        user.GoogleId = identity.Sub;
        user.GoogleEmail = identity.Email;
        await _db.SaveChangesAsync();
        return Ok(new { googleEmail = user.GoogleEmail });
    }

    // DELETE /api/auth/google-link
    [Authorize]
    [HttpDelete("google-link")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        user.GoogleId = null;
        user.GoogleEmail = null;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // POST /api/auth/google  { idToken }  (sign in with a Google account that was linked earlier)
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleTokenRequest req)
    {
        if (!_google.IsConfigured) return StatusCode(503, new { error = "Google sign-in is not configured on the server" });
        var identity = await _google.VerifyAsync(req.IdToken);
        if (identity == null) return Unauthorized(new { error = "Could not verify the Google account" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == identity.Sub);
        if (user == null)
        {
            return Unauthorized(new { error = "No account is linked to this Google account. Sign in with your email, then link Google from Account Settings." });
        }
        return Ok(new AuthResponse(_tokens.CreateToken(user.Id), ToPublic(user)));
    }
}
