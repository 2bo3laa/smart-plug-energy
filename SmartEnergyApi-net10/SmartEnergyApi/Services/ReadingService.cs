using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Everything that happens when a power reading arrives: store it, update the device snapshot and totals,
/// charge the card / tariff tier and raise alerts. Used by the ESP32 endpoint and by the built-in device simulator.
/// </summary>
public class ReadingService
{
    private static readonly ConcurrentDictionary<int, DateTime> TierChecks = new();
    private static readonly ConcurrentDictionary<int, DateTime> MoneyChecks = new();

    private readonly AppDbContext _db;
    private readonly StatisticsService _stats;
    private readonly CardService _card;

    public ReadingService(AppDbContext db, StatisticsService stats, CardService card)
    {
        _db = db;
        _stats = stats;
        _card = card;
    }

    /// <param name="prev">The reading right before this one (pass it when applying several readings in a row).</param>
    /// <param name="batch">When true nothing is saved and no analytics run: the caller saves once and calls AfterIngestAsync.</param>
    public async Task<Reading> ApplyReadingAsync(Device device, IngestReadingRequest req, DateTime ts, Reading? prev = null, bool batch = false)
    {
        prev ??= await _db.Readings.Where(r => r.DeviceId == device.Id).OrderByDescending(r => r.Timestamp).FirstOrDefaultAsync();

        var reading = new Reading
        {
            DeviceId = device.Id,
            Voltage = req.Voltage,
            Current = req.Current,
            Power = req.Power,
            Energy = req.Energy,
            Frequency = req.Frequency,
            PowerFactor = req.PowerFactor,
            Timestamp = ts
        };
        _db.Readings.Add(reading);

        // Update the device's live snapshot (FR7) and today's running totals.
        var deltaKwh = Math.Max(0, req.Energy - device.CumulativeEnergyKwh);
        device.CumulativeEnergyKwh = req.Energy;
        device.TodayKwh += deltaKwh;

        // Card + tariff accounting: energy used since the previous reading (counter delta, sanity-checked against
        // power x time). Running totals are kept on the user/device rows, so nothing has to re-read old readings.
        double billKwh = 0;
        if (prev != null)
        {
            var hours = (reading.Timestamp - prev.Timestamp).TotalHours;
            if (hours > 0 && hours <= 6)
            {
                var byPower = (prev.Power + req.Power) / 2.0 / 1000.0 * hours;
                var plausibleMax = Math.Max(prev.Power, req.Power) / 1000.0 * hours * 1.5 + 0.001;
                billKwh = (deltaKwh > 0 && deltaKwh <= plausibleMax) ? deltaKwh : byPower;
            }
        }
        await _card.RecordConsumptionAsync(device, billKwh);

        device.Voltage = req.Voltage;
        device.Current = req.Current;
        device.Power = req.Power;
        device.Frequency = req.Frequency;
        device.PowerFactor = req.PowerFactor;
        device.IsOnline = true;
        device.LastSeenAt = ts;
        // If the firmware reports the real relay state, mirror it (unless a command is still waiting to be applied).
        if (req.RelayOn is bool relayOn && device.PendingRelayCommand == null) device.IsOn = relayOn;

        // FR9/FR10: raise an alert if an active threshold is crossed.
        // Throttled to one alert per device per 10 minutes so a sustained overload does not flood the user.
        var alertCutoff = DateTime.UtcNow.AddMinutes(-10);
        var activeThreshold = device.Thresholds.FirstOrDefault(t => t.IsActive);
        if (activeThreshold != null && req.Power > activeThreshold.LimitValue)
        {
            if (!await RecentAlertAsync(device.Id, "power", alertCutoff))
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
            if (!await RecentAlertAsync(device.Id, "overload", alertCutoff))
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

        if (!batch)
        {
            await _db.SaveChangesAsync();
            await AfterIngestAsync(device.UserId);
        }
        return reading;
    }

    private async Task<bool> RecentAlertAsync(int deviceId, string type, DateTime cutoff) =>
        await _db.Alerts.AnyAsync(a => a.DeviceId == deviceId && a.Type == type && a.CreatedAt > cutoff)
        || _db.Alerts.Local.Any(a => a.DeviceId == deviceId && a.Type == type && a.CreatedAt > cutoff);

    /// <summary>Slower checks that run after readings are saved (throttled per user).</summary>
    public async Task AfterIngestAsync(int userId)
    {
        // Tariff-tier warning: re-evaluated at most once an hour per user.
        if (!TierChecks.TryGetValue(userId, out var lastCheck) || DateTime.UtcNow - lastCheck > TimeSpan.FromHours(1))
        {
            TierChecks[userId] = DateTime.UtcNow;
            try { await _stats.CheckTierAlertsAsync(userId); } catch { /* never fail an ingest because of analytics */ }
        }

        // Card balance + tier crossing: re-checked at most every 2 minutes per user.
        if (!MoneyChecks.TryGetValue(userId, out var lastMoney) || DateTime.UtcNow - lastMoney > TimeSpan.FromMinutes(2))
        {
            MoneyChecks[userId] = DateTime.UtcNow;
            try
            {
                await _card.CheckTierCrossingAsync(userId);
                await _card.CheckBalanceAlertsAsync(userId);
            }
            catch { /* never fail an ingest because of analytics */ }
        }
    }
}
