using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

/// <summary>Per-user backups: create, list, restore, send by e-mail as a file, copy as text and import from text.</summary>
[ApiController]
[Route("api/backups")]
[Authorize]
public class BackupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BackupService _backups;
    private readonly EmailService _email;

    public BackupsController(AppDbContext db, BackupService backups, EmailService email)
    {
        _db = db;
        _backups = backups;
        _email = email;
    }

    private static BackupDto ToDto(Backup b) => new(b.Id, b.Kind, b.CreatedAt, b.DeviceCount, b.ScheduleCount, b.SizeBytes);

    private Task<Backup?> Mine(int id)
    {
        var userId = User.GetUserId();
        return _db.Backups.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
    }

    // GET /api/backups
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.GetUserId();
        var list = await _db.Backups.Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Select(b => new BackupDto(b.Id, b.Kind, b.CreatedAt, b.DeviceCount, b.ScheduleCount, b.SizeBytes))
            .ToListAsync();
        return Ok(new { backups = list, max = BackupService.MaxPerUser });
    }

    // POST /api/backups  (manual backup now)
    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        var b = await _backups.CreateAsync(user, "manual");
        return StatusCode(201, new { backup = ToDto(b) });
    }

    // POST /api/backups/{id}/restore
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        var b = await Mine(id);
        if (b == null) return NotFound(new { error = "Backup not found" });
        try
        {
            await _backups.RestoreAsync(user, b);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        return Ok(new { success = true });
    }

    // GET /api/backups/{id}/text  (the backup as text, to copy and import later, here or on another account)
    [HttpGet("{id:int}/text")]
    public async Task<IActionResult> Text(int id)
    {
        var b = await Mine(id);
        if (b == null) return NotFound(new { error = "Backup not found" });
        return Ok(new { text = b.Data });
    }

    // POST /api/backups/{id}/email  (sends the backup to the account e-mail as a .json file)
    [HttpPost("{id:int}/email")]
    public async Task<IActionResult> Email(int id, EmailBackupRequest? req)
    {
        if (!_email.IsConfigured) return StatusCode(503, new { error = "Email sending is not configured on the server" });
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        var b = await Mine(id);
        if (b == null) return NotFound(new { error = "Backup not found" });

        var ar = req?.Lang == "ar";
        var local = b.CreatedAt.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var subject = ar ? "نسخة احتياطية من Smart Energy" : "Your Smart Energy backup";
        var html = ar
            ? $"<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\"><p>مرفق ملف النسخة الاحتياطية ({local}).</p><p>للاسترجاع: افتح الملف وانسخ محتواه، ثم من التطبيق: الإعدادات ← النسخ الاحتياطي ← استيراد من نص.</p></div>"
            : $"<div style=\"font-family:Arial,sans-serif\"><p>Your backup file is attached ({local}).</p><p>To restore it: open the file and copy its content, then in the app go to Settings &gt; Backups &gt; Import from text.</p></div>";
        try
        {
            await _email.SendAsync(user.Email, subject, html,
                new EmailAttachment(BackupService.FileName(b), System.Text.Encoding.UTF8.GetBytes(b.Data), "application/json"));
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not send the email: " + ex.Message });
        }
        return Ok(new { message = "Backup sent" });
    }

    // POST /api/backups/import  { text }  (adds the pasted backup to the list; restore it afterwards)
    [HttpPost("import")]
    public async Task<IActionResult> Import(ImportBackupRequest req)
    {
        try
        {
            var b = await _backups.ImportAsync(User.GetUserId(), req.Text);
            return StatusCode(201, new { backup = ToDto(b) });
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
