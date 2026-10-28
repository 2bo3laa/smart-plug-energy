using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlertsController(AppDbContext db)
    {
        _db = db;
    }

    private static AlertDto ToDto(Alert a) => new(a.Id, a.DeviceId, a.Type, a.Title, a.Message, a.Severity, a.IsRead, a.CreatedAt);

    // GET /api/alerts?severity=warning|error|info|success
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? severity)
    {
        var userId = User.GetUserId();
        var query = _db.Alerts.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(a => a.Severity == severity);
        }

        var alerts = await query.OrderByDescending(a => a.Id).ToListAsync();
        var unreadCount = await _db.Alerts.CountAsync(a => a.UserId == userId && !a.IsRead);

        return Ok(new AlertListResponse(alerts.Select(ToDto).ToList(), unreadCount));
    }

    // PATCH /api/alerts/{id}/read
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.GetUserId();
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (alert == null) return NotFound(new { error = "Alert not found" });

        alert.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { alert = ToDto(alert) });
    }

    // PATCH /api/alerts/read-all
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.GetUserId();
        var unread = await _db.Alerts.Where(a => a.UserId == userId && !a.IsRead).ToListAsync();
        foreach (var a in unread) a.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // DELETE /api/alerts/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (alert == null) return NotFound(new { error = "Alert not found" });

        _db.Alerts.Remove(alert);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
