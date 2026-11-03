using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

/// <summary>E-mail reports: the user chooses weekly / monthly in Settings; a scheduled GitHub job calls /run.</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ReportService _reports;
    private readonly IConfiguration _config;

    public ReportsController(AppDbContext db, ReportService reports, IConfiguration config)
    {
        _db = db;
        _reports = reports;
        _config = config;
    }

    // GET /api/reports/settings
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        return Ok(new ReportSettingsDto(user.ReportWeekly, user.ReportMonthly, user.Email, _reports.MailConfigured, user.ReportLang ?? "en"));
    }

    // PUT /api/reports/settings
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SaveReportSettingsRequest req)
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        user.ReportWeekly = req.Weekly;
        user.ReportMonthly = req.Monthly;
        user.ReportLang = req.Lang == "ar" ? "ar" : "en";
        await _db.SaveChangesAsync();
        return Ok(new ReportSettingsDto(user.ReportWeekly, user.ReportMonthly, user.Email, _reports.MailConfigured, user.ReportLang));
    }

    // POST /api/reports/send-now  { period, lang }  -> sends one report to the signed-in user right away
    [HttpPost("send-now")]
    public async Task<IActionResult> SendNow(SendReportNowRequest req)
    {
        if (!_reports.MailConfigured) return StatusCode(503, new { error = "Email sending is not configured on the server" });
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound(new { error = "User not found" });
        var period = req.Period == "monthly" ? "monthly" : "weekly";
        var lang = req.Lang == "ar" ? "ar" : "en";
        try
        {
            await _reports.SendReportAsync(user, period, mtd: true, lang);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not send the email: " + ex.Message });
        }
        return Ok(new { message = "Report sent" });
    }

    // POST /api/reports/run?period=weekly|monthly   (called by the scheduled GitHub workflow, header X-Cron-Key)
    [HttpPost("run")]
    [AllowAnonymous]
    public async Task<IActionResult> Run([FromQuery] string? period, [FromHeader(Name = "X-Cron-Key")] string? key)
    {
        var expected = _config["Reports:CronKey"];
        if (string.IsNullOrEmpty(expected)) return StatusCode(503, new { error = "Reports:CronKey is not set on the server" });
        if (key != expected) return Unauthorized(new { error = "Invalid key" });
        if (!_reports.MailConfigured) return StatusCode(503, new { error = "Email sending is not configured on the server" });

        var monthly = period == "monthly";
        var minGap = monthly ? TimeSpan.FromDays(20) : TimeSpan.FromDays(5);
        var now = DateTime.UtcNow;

        var users = monthly
            ? await _db.Users.Where(u => u.ReportMonthly && u.DeletedAt == null).ToListAsync()
            : await _db.Users.Where(u => u.ReportWeekly && u.DeletedAt == null).ToListAsync();

        int sent = 0, skipped = 0, failed = 0;
        foreach (var u in users)
        {
            var last = monthly ? u.LastMonthlyReportAt : u.LastWeeklyReportAt;
            if (last != null && now - last.Value < minGap) { skipped++; continue; }
            try
            {
                await _reports.SendReportAsync(u, monthly ? "monthly" : "weekly", mtd: false, u.ReportLang ?? "en");
                if (monthly) u.LastMonthlyReportAt = now; else u.LastWeeklyReportAt = now;
                await _db.SaveChangesAsync();
                sent++;
            }
            catch
            {
                failed++;
            }
        }
        return Ok(new { sent, skipped, failed });
    }
}
