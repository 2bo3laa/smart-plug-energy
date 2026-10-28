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
    private static readonly ConcurrentDictionary<int, DateTime> _tierChecks = new();

    public DevicesController(AppDbContext db, StatisticsService stats)
    {
        _db = db;
        _stats = stats;
    }

    private static DeviceDto ToDto(Device d) => new(
        d.Id, d.Name, d.Room, d.IsOnline, d.IsOn,
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
        var devices = await _db.Devices.Where(d => d.UserId == userId).OrderBy(d => d.Id).ToListAsync();
        return Ok(new DeviceListResponse(
            devices.Select(ToDto).ToList(),
            devices.Count,
            devices.Count(d => d.IsOnline),
            devices.Count(d => !d.IsOnline)
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
        var device = new Device { UserId = userId, Name = req.Name.Trim(), Room = req.Room.Trim(), IsOnline = true, IsOn = false,
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
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (d == null) return NotFound(new { error = "Device not found" });

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

        var reading = new Reading
        {
            DeviceId = device.Id,
            Voltage = req.Voltage,
            Current = req.Current,
            Power = req.Power,
            Energy = req.Energy,
            Frequency = req.Frequency,
            PowerFactor = req.PowerFactor,
            Timestamp = DateTime.UtcNow
        };
        _db.Readings.Add(reading);

        // Update the device's live snapshot (FR7) and today's running totals.
        var deltaKwh = Math.Max(0, req.Energy - device.CumulativeEnergyKwh);
        device.CumulativeEnergyKwh = req.Energy;
        device.TodayKwh += deltaKwh;
        device.Voltage = req.Voltage;
        device.Current = req.Current;
        device.Power = req.Power;
        device.Frequency = req.Frequency;
        device.PowerFactor = req.PowerFactor;
        device.IsOnline = true;

        // FR9/FR10: raise an alert if an active threshold is crossed.
        // Throttled to one alert per device per 10 minutes so a sustained overload does not flood the user.
        var alertCutoff = DateTime.UtcNow.AddMinutes(-10);
        var activeThreshold = device.Thresholds.FirstOrDefault(t => t.IsActive);
        if (activeThreshold != null && req.Power > activeThreshold.LimitValue)
        {
            var recent = await _db.Alerts.AnyAsync(a => a.DeviceId == device.Id && a.Type == "power" && a.CreatedAt > alertCutoff);
            if (!recent)
            {
                _db.Alerts.Add(new Alert
                {
                    UserId = device.UserId,
                    DeviceId = device.Id,
                    Type = "power",
                    Title = "High Power Usage",
                    Message = $"{device.Name} exceeded {activeThreshold.LimitValue}W (reading: {req.Power}W)",
                    Severity = "error"
                });
            }
        }

        // Rated-power check: drawing well above the appliance nameplate usually means a fault.
        if (device.RatedWatts is double rated && rated > 0 && req.Power > rated * 1.2)
        {
            var recent = await _db.Alerts.AnyAsync(a => a.DeviceId == device.Id && a.Type == "overload" && a.CreatedAt > alertCutoff);
            if (!recent)
            {
                var pct = (int)Math.Round((req.Power / rated - 1) * 100);
                _db.Alerts.Add(new Alert
                {
                    UserId = device.UserId,
                    DeviceId = device.Id,
                    Type = "overload",
                    Title = "Above rated power",
                    Message = $"{device.Name} is drawing {req.Power:0}W, {pct}% above its rated {rated:0}W. Check the appliance.",
                    Severity = "warning"
                });
            }
        }

        await _db.SaveChangesAsync();

        // Tariff-tier warning: re-evaluated at most once an hour per user.
        if (!_tierChecks.TryGetValue(device.UserId, out var lastCheck) || DateTime.UtcNow - lastCheck > TimeSpan.FromHours(1))
        {
            _tierChecks[device.UserId] = DateTime.UtcNow;
            try { await _stats.CheckTierAlertsAsync(device.UserId); } catch { /* never fail an ingest because of analytics */ }
        }

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
