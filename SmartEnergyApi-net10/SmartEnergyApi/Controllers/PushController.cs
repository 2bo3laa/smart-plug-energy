using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

/// <summary>Phone notifications: the app registers its Firebase token here; alerts are then pushed to that phone.</summary>
[ApiController]
[Route("api/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PushService _push;

    public PushController(AppDbContext db, PushService push)
    {
        _db = db;
        _push = push;
    }

    // POST /api/push/register  { token, lang }
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterPushRequest req)
    {
        var token = (req.Token ?? "").Trim();
        if (token.Length < 20 || token.Length > 400) return BadRequest(new { error = "Invalid push token" });
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });

        // A phone belongs to one account at a time.
        var others = await _db.Users.Where(u => u.PushToken == token && u.Id != userId).ToListAsync();
        foreach (var o in others) o.PushToken = null;

        user.PushToken = token;
        user.PushLang = req.Lang == "ar" ? "ar" : "en";
        await _db.SaveChangesAsync();
        return Ok(new { configured = _push.IsConfigured });
    }

    // DELETE /api/push/register  (on log out)
    [HttpDelete("register")]
    public async Task<IActionResult> Unregister()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user != null)
        {
            user.PushToken = null;
            await _db.SaveChangesAsync();
        }
        return Ok(new { message = "Unregistered" });
    }

    // POST /api/push/test  -> sends a test notification to the signed-in user's phone
    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        if (!_push.IsConfigured) return StatusCode(503, new { error = "Push notifications are not configured on the server" });
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        if (string.IsNullOrEmpty(user.PushToken)) return BadRequest(new { error = "No phone is registered yet. Allow notifications in the app and try again." });

        var ar = user.PushLang == "ar";
        var result = await _push.SendAsync(
            user.PushToken,
            "Smart Energy",
            ar ? "إشعار تجريبي — الإشعارات شغالة على تليفونك." : "Test notification — phone notifications are working.",
            new Dictionary<string, string> { ["type"] = "test" });
        if (result == PushResult.InvalidToken)
        {
            user.PushToken = null;
            await _db.SaveChangesAsync();
            return BadRequest(new { error = "This phone is no longer registered. Open the app again and retry." });
        }
        if (result != PushResult.Sent) return StatusCode(502, new { error = "Could not send the notification: " + (_push.LastError ?? "unknown error") });
        return Ok(new { message = "Test sent" });
    }
}
