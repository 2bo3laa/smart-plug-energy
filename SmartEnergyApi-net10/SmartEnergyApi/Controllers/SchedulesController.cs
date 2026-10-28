using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SchedulesController(AppDbContext db)
    {
        _db = db;
    }

    private static ScheduleDto ToDto(Schedule s) => new(s.Id, s.DeviceId, s.Label, s.Time, s.Days, s.Type, s.IsActive);

    private async Task<bool> OwnsDeviceAsync(int deviceId, int userId) =>
        await _db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId);

    // GET /api/devices/{deviceId}/schedules
    [HttpGet("devices/{deviceId:int}/schedules")]
    public async Task<IActionResult> GetForDevice(int deviceId)
    {
        var userId = User.GetUserId();
        if (!await OwnsDeviceAsync(deviceId, userId)) return NotFound(new { error = "Device not found" });

        var schedules = await _db.Schedules
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Id)
            .ToListAsync();

        return Ok(new { schedules = schedules.Select(ToDto).ToList() });
    }

    // POST /api/devices/{deviceId}/schedules
    [HttpPost("devices/{deviceId:int}/schedules")]
    public async Task<IActionResult> Create(int deviceId, CreateScheduleRequest req)
    {
        var userId = User.GetUserId();
        if (!await OwnsDeviceAsync(deviceId, userId)) return NotFound(new { error = "Device not found" });

        if (string.IsNullOrWhiteSpace(req.Label) || string.IsNullOrWhiteSpace(req.Time) ||
            string.IsNullOrWhiteSpace(req.Days) || string.IsNullOrWhiteSpace(req.Type))
        {
            return BadRequest(new { error = "label, time, days and type are required" });
        }

        var schedule = new Schedule
        {
            DeviceId = deviceId,
            Label = req.Label,
            Time = req.Time,
            Days = req.Days,
            Type = req.Type,
            IsActive = true
        };
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        return StatusCode(201, new { schedule = ToDto(schedule) });
    }

    private async Task<Schedule?> FindOwnedScheduleAsync(int scheduleId, int userId)
    {
        return await _db.Schedules
            .Join(_db.Devices, s => s.DeviceId, d => d.Id, (s, d) => new { s, d })
            .Where(x => x.s.Id == scheduleId && x.d.UserId == userId)
            .Select(x => x.s)
            .FirstOrDefaultAsync();
    }

    // PATCH /api/schedules/{id}  (edit label/time/days/type)
    [HttpPatch("schedules/{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateScheduleRequest req)
    {
        var userId = User.GetUserId();
        var schedule = await FindOwnedScheduleAsync(id, userId);
        if (schedule == null) return NotFound(new { error = "Schedule not found" });

        if (!string.IsNullOrWhiteSpace(req.Label)) schedule.Label = req.Label;
        if (!string.IsNullOrWhiteSpace(req.Time)) schedule.Time = req.Time;
        if (!string.IsNullOrWhiteSpace(req.Days)) schedule.Days = req.Days;
        if (!string.IsNullOrWhiteSpace(req.Type)) schedule.Type = req.Type;

        await _db.SaveChangesAsync();
        return Ok(new { schedule = ToDto(schedule) });
    }

    // DELETE /api/schedules/{id}
    [HttpDelete("schedules/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var schedule = await FindOwnedScheduleAsync(id, userId);
        if (schedule == null) return NotFound(new { error = "Schedule not found" });

        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // PATCH /api/schedules/{id}/toggle
    [HttpPatch("schedules/{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = User.GetUserId();
        var schedule = await FindOwnedScheduleAsync(id, userId);
        if (schedule == null) return NotFound(new { error = "Schedule not found" });

        schedule.IsActive = !schedule.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { schedule = ToDto(schedule) });
    }
}
