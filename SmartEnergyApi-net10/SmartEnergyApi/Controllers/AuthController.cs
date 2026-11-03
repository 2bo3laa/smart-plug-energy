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
    private readonly AccountService _accounts;
    private readonly bool _exposeResetCode;
    private readonly bool _seedDemo;

    public AuthController(AppDbContext db, TokenService tokens, ILogger<AuthController> logger, GoogleAuthService google, AccountService accounts,
        IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
        _google = google;
        _accounts = accounts;
        // The reset code is only returned to the client in Development or when Auth:ExposeResetCode=true
        // (demo only: there is no email provider, and returning it lets anyone reset any account).
        // Fake demo devices/alerts are NOT created for new accounts any more (Demo:SeedOnSignup=true brings them back).
        _seedDemo = string.Equals(config["Demo:SeedOnSignup"], "true", StringComparison.OrdinalIgnoreCase);
        _exposeResetCode = env.IsDevelopment() || string.Equals(config["Auth:ExposeResetCode"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static PublicUserDto ToPublic(User u) => new(u.Id, u.Name, u.Email);

    public const string ScheduledCode = "account_scheduled_for_deletion";

    /// <summary>403 for an account that is waiting for its 30-day deletion: the only thing it can do is restore itself.</summary>
    private IActionResult ScheduledForDeletion(User u, int status = StatusCodes.Status403Forbidden) =>
        StatusCode(status, new { error = "Account scheduled for deletion", code = ScheduledCode, purgeAt = u.PurgeAt });

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
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing != null && existing.PurgeAt != null && existing.PurgeAt <= DateTime.UtcNow)
        {
            // Its 30 days are over and the nightly wipe has not reached it yet: wipe it now so the e-mail can be used again.
            await _accounts.PurgeAsync(existing.Id);
            existing = null;
        }
        if (existing != null)
        {
            if (existing.IsScheduledForDeletion) return ScheduledForDeletion(existing, StatusCodes.Status409Conflict);
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

        // A new account starts empty: the user adds devices from the app (they behave like real ones, see SimulationService).
        if (_seedDemo) SeedData.SeedForUser(_db, user.Id);

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
        if (user.IsScheduledForDeletion)
        {
            if (user.PurgeAt <= DateTime.UtcNow) return Unauthorized(new { error = "Invalid email or password" });
            return ScheduledForDeletion(user);   // the app offers "Restore account"
        }

        var token = _tokens.CreateToken(user.Id);
        return Ok(new AuthResponse(token, ToPublic(user)));
    }

    // ---- Delete account (30-day restore window) ----

    // POST /api/auth/delete-account  { email, password }
    // Confirms with the e-mail and the password, then schedules the deletion: the account is blocked straight away
    // and everything is wiped for good after DeletionGraceDays unless the user restores it first.
    [Authorize]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { error = "email and password are required" });
        }
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        if (!string.Equals(req.Email.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "The email does not match this account" });
        }
        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Password is incorrect" });
        }

        var now = DateTime.UtcNow;
        user.DeletedAt = now;
        user.PurgeAt = now.AddDays(AccountService.DeletionGraceDays);
        user.PushToken = null;   // no phone notifications while the account is waiting for deletion
        await _db.SaveChangesAsync();
        return Ok(new { success = true, purgeAt = user.PurgeAt });
    }

    // POST /api/auth/restore-account  { email, password }  or  { idToken } (Google account)  -> signs the user back in
    [HttpPost("restore-account")]
    public async Task<IActionResult> RestoreAccount(RestoreAccountRequest req)
    {
        User? user = null;
        if (!string.IsNullOrWhiteSpace(req.Email) && !string.IsNullOrWhiteSpace(req.Password))
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var found = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (found != null && BCrypt.Net.BCrypt.Verify(req.Password, found.PasswordHash)) user = found;
        }
        else if (!string.IsNullOrWhiteSpace(req.IdToken))
        {
            if (!_google.IsConfigured) return StatusCode(503, new { error = "Google sign-in is not configured on the server" });
            var identity = await _google.VerifyAsync(req.IdToken);
            if (identity != null) user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == identity.Sub);
        }
        else
        {
            return BadRequest(new { error = "email and password are required" });
        }
        if (user == null) return Unauthorized(new { error = "Invalid email or password" });

        if (!user.IsScheduledForDeletion) return BadRequest(new { error = "This account is not scheduled for deletion" });
        if (user.PurgeAt <= DateTime.UtcNow) return StatusCode(StatusCodes.Status410Gone, new { error = "The 30-day restore period has ended" });

        user.DeletedAt = null;
        user.PurgeAt = null;
        await _db.SaveChangesAsync();
        return Ok(new AuthResponse(_tokens.CreateToken(user.Id), ToPublic(user)));
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
            // Google already verified this e-mail address. If an account with it exists, link it and sign in;
            // otherwise this is a new user: create the account right away (one-tap sign-up).
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);
            if (user != null && user.IsScheduledForDeletion) return ScheduledForDeletion(user);
            if (user != null)
            {
                user.GoogleId = identity.Sub;
                user.GoogleEmail = identity.Email;
                await _db.SaveChangesAsync();
            }
            else
            {
                user = new User
                {
                    Name = string.IsNullOrWhiteSpace(identity.Name) ? identity.Email.Split('@')[0] : identity.Name.Trim(),
                    Email = identity.Email,
                    // Random password nobody knows: this account signs in with Google (or resets it with "Forgot password").
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)), workFactor: 10),
                    GoogleId = identity.Sub,
                    GoogleEmail = identity.Email
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                if (_seedDemo) SeedData.SeedForUser(_db, user.Id);
            }
        }
        if (user.IsScheduledForDeletion)
        {
            if (user.PurgeAt <= DateTime.UtcNow) return Unauthorized(new { error = "Could not verify the Google account" });
            return ScheduledForDeletion(user);
        }
        return Ok(new AuthResponse(_tokens.CreateToken(user.Id), ToPublic(user)));
    }
}
