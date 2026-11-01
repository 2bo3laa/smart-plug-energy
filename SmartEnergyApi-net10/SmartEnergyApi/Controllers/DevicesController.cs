using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;
using System.Collections.Concurrent;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StatisticsService _stats;
    private readonly CardService _card;

    private readonly TimeSpan _offlineAfter;
    private readonly ReadingService _readings;
    private readonly SimulationService _sim;

    public DevicesController(AppDbContext db, StatisticsService stats, CardService card, IConfiguration config,
        ReadingService readings, SimulationService sim)
    {
        _db = db;
        _stats = stats;
        _card = card;
        _readings = readings;
        _sim = sim;
        // A device that has reported before counts as offline once it has been silent this long
        // (keep it at least ~3x the ESP32 posting interval).
        _offlineAfter = TimeSpan.FromSeconds(int.TryParse(config["Device:OfflineAfterSeconds"], out var secs) && secs > 0 ? secs : 90);
    }

    private bool IsOnlineNow(Device d) =>
        d.LastSeenAt is DateTime seen ? DateTime.UtcNow - seen <= _offlineAfter : d.IsOnline;

    private DeviceDto ToDto(Device d) => new(
        d.Id, d.Name, d.Room, IsOnlineNow(d), d.IsOn,
        d.Voltage, d.Current, d.Power, d.PowerFactor, d.TodayKwh, d.TodayCost, d.RatedWatts,
        d.Type, d.Brand, d.Model
    );

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private async Task<bool> KnownType(string? key) =>
        key == null || await _db.ApplianceTypes.AnyAsync(t => t.Key == key);

    // GET /api/devices
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);   // virtual plugs: catch up their readings
        var devices = await _db.Devices.Where(d => d.UserId == userId).OrderBy(d => d.Id).ToListAsync();
        return Ok(new DeviceListResponse(
            devices.Select(ToDto).ToList(),
            devices.Count,
            devices.Count(d => IsOnlineNow(d)),
            devices.Count(d => !IsOnlineNow(d))
        ));
    }

    // GET /api/devices/scan  (mock nearby-device discovery for the Add Device screen)
    [HttpGet("scan")]
    public IActionResult Scan()
    {
        return Ok(new ScanResponse(new List<ScanResultDto>
        {
            new("scan-04", "Smart Plug 04", "Strong signal"),
            new("scan-05", "Smart Plug 05", "Medium signal"),
        }));
    }

    // GET /api/devices/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (d == null) return NotFound(new { error = "Device not found" });
        return Ok(new { device = ToDto(d) });
    }

    // POST /api/devices  (finish device setup: name + room)
    [HttpPost]
    public async Task<IActionResult> Create(CreateDeviceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Room))
        {
            return BadRequest(new { error = "name and room are required" });
        }

        var type = Clean(req.Type);
        if (!await KnownType(type)) return BadRequest(new { error = "Unknown appliance type" });

        var userId = User.GetUserId();
        var device = new Device { UserId = userId, Name = req.Name.Trim(), Room = req.Room.Trim(), IsOnline = true, IsOn = false, IsSimulated = _sim.Enabled,
            Type = type, Brand = Clean(req.Brand), Model = Clean(req.Model),
            RatedWatts = (req.RatedWatts is > 0 and <= 20000) ? req.RatedWatts : null };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { device = ToDto(device) });
    }

    // PATCH /api/devices/{id}  (rename device and/or move it to another room)
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateDeviceRequest req)
    {
        var userId = User.GetUserId();
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (d == null) return NotFound(new { error = "Device not found" });

        if (string.IsNullOrWhiteSpace(req.Name) && string.IsNullOrWhiteSpace(req.Room) && req.RatedWatts == null
            && req.Type == null && req.Brand == null && req.Model == null)
        {
            return BadRequest(new { error = "name, room, ratedWatts, type, brand or model is required" });
        }
        if (req.RatedWatts is > 20000)
        {
            return BadRequest(new { error = "ratedWatts must be 20000 or less" });
        }

        if (!string.IsNullOrWhiteSpace(req.Name)) d.Name = req.Name.Trim();
        if (!string.IsNullOrWhiteSpace(req.Room)) d.Room = req.Room.Trim();
        if (req.RatedWatts is double rw) d.RatedWatts = rw > 0 ? rw : null; // 0 clears it
        if (req.Type != null)
        {
            var type = Clean(req.Type);
            if (!await KnownType(type)) return BadRequest(new { error = "Unknown appliance type" });
            d.Type = type;
        }
        if (req.Brand != null) d.Brand = Clean(req.Brand); // empty string clears
        if (req.Model != null) d.Model = Clean(req.Model);
        await _db.SaveChangesAsync();

        return Ok(new { device = ToDto(d) });
    }

    // DELETE /api/devices/{id}  (also removes schedules/readings/thresholds; alerts keep history with deviceId = null)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (d == null) return NotFound(new { error = "Device not found" });

        _db.Devices.Remove(d);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // POST /api/devices/{id}/toggle  (manual on/off from the app - FR16)
    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);   // account for the time spent in the old state before switching
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (d == null) return NotFound(new { error = "Device not found" });

        if (!IsOnlineNow(d)) return Conflict(new { error = "Device is offline" });

        var newState = !d.IsOn;
        d.IsOn = newState;
        // Queue the command for the ESP32 to pick up next time it polls (FR17); it also
        // applies immediately in the DB snapshot above so the app feels instant.
        d.PendingRelayCommand = newState;

        _db.Alerts.Add(new Alert
        {
            UserId = userId,
            DeviceId = d.Id,
            Type = newState ? "device_on" : "device_off",
            Title = newState ? "Device Turned ON" : "Device Turned OFF",
            Message = $"{d.Name} was turned {(newState ? "on" : "off")}",
            Severity = newState ? "success" : "info"
        });

        await _db.SaveChangesAsync();
        return Ok(new { device = ToDto(d) });
    }

    // ---- Real hardware endpoints (ESP32 firmware side, authenticated with the device's own key) ----

    // POST /api/devices/{id}/readings  (ESP32 pushes a PZEM-004T V3 measurement - FR4/FR5/FR6)
    [AllowAnonymous]
    [HttpPost("{id:int}/readings")]
    public async Task<IActionResult> IngestReading(int id, IngestReadingRequest req, [FromHeader(Name = "X-Device-Key")] string? deviceKey)
    {
        var device = await _db.Devices.Include(d => d.Thresholds).FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound(new { error = "Device not found" });
        if (string.IsNullOrEmpty(deviceKey) || deviceKey != device.DeviceKey)
        {
            return Unauthorized(new { error = "Invalid or missing device key" });
        }

        device.IsSimulated = false;   // real hardware took over: stop simulating this device
        await _readings.ApplyReadingAsync(device, req, DateTime.UtcNow);
        return StatusCode(201, new { success = true });
    }

    // GET /api/devices/{id}/readings?take=200  (historical readings for charts)
    [HttpGet("{id:int}/readings")]
    public async Task<IActionResult> GetReadings(int id, [FromQuery] int take = 200)
    {
        var userId = User.GetUserId();
        var owns = await _db.Devices.AnyAsync(d => d.Id == id && d.UserId == userId);
        if (!owns) return NotFound(new { error = "Device not found" });

        take = Math.Clamp(take, 1, 2000);
        var readings = await _db.Readings
            .Where(r => r.DeviceId == id)
            .OrderByDescending(r => r.Timestamp)
            .Take(take)
            .OrderBy(r => r.Timestamp)
            .Select(r => new ReadingDto(r.Id, r.Voltage, r.Current, r.Power, r.Energy, r.Frequency, r.PowerFactor, r.Timestamp))
            .ToListAsync();

        return Ok(new { readings });
    }

    // GET /api/devices/{id}/command  (ESP32 polls this to learn about a pending relay command - FR17)
    [AllowAnonymous]
    [HttpGet("{id:int}/command")]
    public async Task<IActionResult> GetPendingCommand(int id, [FromHeader(Name = "X-Device-Key")] string? deviceKey)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device == null) return NotFound(new { error = "Device not found" });
        if (string.IsNullOrEmpty(deviceKey) || deviceKey != device.DeviceKey)
        {
            return Unauthorized(new { error = "Invalid or missing device key" });
        }

        device.LastSeenAt = DateTime.UtcNow;   // a poll also proves the device is alive
        device.IsOnline = true;
        var command = device.PendingRelayCommand;
        device.PendingRelayCommand = null; // one-shot: cleared once the firmware has fetched it
        await _db.SaveChangesAsync();

        return Ok(new RelayCommandDto(command));
    }

    // ---- Thresholds (FR9) ----

    [HttpGet("{id:int}/threshold")]
    public async Task<IActionResult> GetThreshold(int id)
    {
        var userId = User.GetUserId();
        var device = await _db.Devices.Include(d => d.Thresholds).FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (device == null) return NotFound(new { error = "Device not found" });

        var threshold = device.Thresholds.OrderByDescending(t => t.Id).FirstOrDefault();
        if (threshold == null) return Ok(new { threshold = (ThresholdDto?)null });
        return Ok(new { threshold = new ThresholdDto(threshold.Id, threshold.DeviceId, threshold.LimitValue, threshold.IsActive) });
    }

    [HttpPut("{id:int}/threshold")]
    public async Task<IActionResult> UpsertThreshold(int id, UpsertThresholdRequest req)
    {
        var userId = User.GetUserId();
        var device = await _db.Devices.Include(d => d.Thresholds).FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (device == null) return NotFound(new { error = "Device not found" });

        var threshold = device.Thresholds.OrderByDescending(t => t.Id).FirstOrDefault();
        if (threshold == null)
        {
            threshold = new Threshold { DeviceId = id };
            _db.Thresholds.Add(threshold);
        }
        threshold.LimitValue = req.LimitValue;
        threshold.IsActive = req.IsActive ?? true;

        await _db.SaveChangesAsync();
        return Ok(new { threshold = new ThresholdDto(threshold.Id, threshold.DeviceId, threshold.LimitValue, threshold.IsActive) });
    }
}
