using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Turns the raw Reading time series into the aggregates the mobile app's
/// dashboard needs. This replaces the seeded-PRNG placeholder numbers from
/// the original prototype with real arithmetic over stored sensor data,
/// falling back to each device's last-known snapshot only when a device has
/// no reading history yet (e.g. it was just registered).
/// </summary>
public class StatisticsService
{
    private readonly AppDbContext _db;
    private readonly TariffService _tariff;

    private static readonly Dictionary<string, (int buckets, TimeSpan window, TimeSpan bucketSize)> RangeConfig = new()
    {
        ["day"] = (8, TimeSpan.FromHours(24), TimeSpan.FromHours(3)),
        ["week"] = (7, TimeSpan.FromDays(7), TimeSpan.FromDays(1)),
        ["month"] = (4, TimeSpan.FromDays(28), TimeSpan.FromDays(7)),
        ["year"] = (12, TimeSpan.FromDays(365), TimeSpan.FromDays(30.4)),
    };

    public StatisticsService(AppDbContext db, TariffService tariff)
    {
        _db = db;
        _tariff = tariff;
    }

    public async Task<StatisticsResponse> BuildAsync(int userId, string requestedRange)
    {
        var range = RangeConfig.ContainsKey(requestedRange) ? requestedRange : "day";
        var (bucketCount, window, bucketSize) = RangeConfig[range];

        var devices = await _db.Devices.Where(d => d.UserId == userId).ToListAsync();
        var deviceIds = devices.Select(d => d.Id).ToList();

        var now = DateTime.UtcNow;
        var windowStart = now - window;

        var readings = await _db.Readings
            .Where(r => deviceIds.Contains(r.DeviceId) && r.Timestamp >= windowStart)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();

        // --- Chart: average power per bucket, converted to kWh consumed in that bucket ---
        var chart = new List<double>();
        for (int i = 0; i < bucketCount; i++)
        {
            var bucketStart = now - window + TimeSpan.FromTicks(window.Ticks * i / bucketCount);
            var bucketEnd = now - window + TimeSpan.FromTicks(window.Ticks * (i + 1) / bucketCount);
            var inBucket = readings.Where(r => r.Timestamp >= bucketStart && r.Timestamp < bucketEnd).ToList();

            double bucketKwh;
            if (inBucket.Count > 0)
            {
                var avgPowerWatts = inBucket.Average(r => r.Power);
                var bucketHours = (bucketEnd - bucketStart).TotalHours;
                bucketKwh = avgPowerWatts / 1000.0 * bucketHours;
            }
            else
            {
                bucketKwh = 0;
            }
            chart.Add(Math.Round(bucketKwh, 2));
        }

        // --- "Today" totals: prefer real readings from the last 24h, fall back to device snapshots ---
        var last24h = readings.Where(r => r.Timestamp >= now.AddHours(-24)).ToList();
        double energyToday;
        if (last24h.Count > 0)
        {
            energyToday = EnergyFromReadings(last24h);
        }
        else
        {
            energyToday = devices.Sum(d => d.TodayKwh);
        }

        // --- Previous 24h window, for the day-over-day change indicator ---
        var previous24h = readings.Where(r => r.Timestamp >= now.AddHours(-48) && r.Timestamp < now.AddHours(-24)).ToList();
        double energyYesterday = previous24h.Count > 0 ? EnergyFromReadings(previous24h) : energyToday;
        double changeVsYesterday = energyYesterday > 0
            ? Math.Round((energyToday - energyYesterday) / energyYesterday * 100.0, 1)
            : 0;

        // --- Month-to-date totals (used for the tiered cost estimate) ---
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthReadings = await _db.Readings
            .Where(r => deviceIds.Contains(r.DeviceId) && r.Timestamp >= monthStart)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();
        double totalEnergyMonth = monthReadings.Count > 0
            ? EnergyFromReadings(monthReadings)
            : energyToday * DateTime.DaysInMonth(now.Year, now.Month);

        double totalCostMonth = await _tariff.EstimateCostAsync(totalEnergyMonth);
        // Blend a per-kWh rate from the monthly estimate so "today" and chart buckets get a consistent cost basis.
        double blendedRate = totalEnergyMonth > 0 ? totalCostMonth / totalEnergyMonth : 1.65;
        double todayCost = Math.Round(energyToday * blendedRate, 2);

        // --- Appliance/device breakdown: real share of energy per device, not a hardcoded guess ---
        var breakdown = BuildDeviceBreakdown(devices, readings);
        var forecast = await BuildForecastAsync(now, monthStart, monthReadings, totalEnergyMonth);

        return new StatisticsResponse(
            range,
            Math.Round(energyToday, 2),
            todayCost,
            changeVsYesterday,
            chart,
            Math.Round(totalEnergyMonth, 2),
            Math.Round(totalCostMonth, 2),
            breakdown,
            forecast
        );
    }

    private async Task<ForecastDto> BuildForecastAsync(DateTime now, DateTime monthStart, List<Reading> monthReadings, double totalEnergyMonth)
    {
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        double daysElapsed = Math.Max((now - monthStart).TotalDays, 0.01);
        double projected = totalEnergyMonth;
        if (monthReadings.Count > 1)
        {
            double observed = (monthReadings[^1].Timestamp - monthReadings[0].Timestamp).TotalDays;
            observed = Math.Max(Math.Min(observed, daysElapsed), 1.0 / 24);
            double dailyAvg = totalEnergyMonth / observed;
            double remainingDays = Math.Max(0, daysInMonth - daysElapsed);
            projected = totalEnergyMonth + dailyAvg * remainingDays;
        }
        double projectedCost = await _tariff.EstimateCostAsync(projected);
        var info = await _tariff.GetTierInfoAsync(totalEnergyMonth);
        bool willCross = info.KwhUntilNext.HasValue && projected >= totalEnergyMonth + info.KwhUntilNext.Value;
        return new ForecastDto(
            Math.Round(projected, 2), projectedCost, info.CurrentPrice, info.NextPrice,
            info.KwhUntilNext.HasValue ? (double?)Math.Round(info.KwhUntilNext.Value, 2) : null, willCross);
    }

    /// <summary>Raises (at most one per 24 h) a warning when the month is on pace to enter a pricier tariff tier.</summary>
    public async Task CheckTierAlertsAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var deviceIds = await _db.Devices.Where(d => d.UserId == userId).Select(d => d.Id).ToListAsync();
        var monthReadings = await _db.Readings
            .Where(r => deviceIds.Contains(r.DeviceId) && r.Timestamp >= monthStart)
            .OrderBy(r => r.Timestamp).ToListAsync();
        if (monthReadings.Count < 2) return;
        var total = EnergyFromReadings(monthReadings);
        var f = await BuildForecastAsync(now, monthStart, monthReadings, total);
        if (!f.WillCrossNextTier || f.NextPricePerKwh == null) return;
        var cutoff = now.AddHours(-24);
        if (await _db.Alerts.AnyAsync(a => a.UserId == userId && a.Type == "tier" && a.CreatedAt > cutoff)) return;
        _db.Alerts.Add(new Alert
        {
            UserId = userId, Type = "tier", Severity = "warning",
            Title = "Approaching a higher tariff tier",
            Message = $"At this pace you will use about {f.ProjectedEnergyMonth:0} kWh this month and enter a tier at {f.NextPricePerKwh:0.00} EGP/kWh ({f.KwhUntilNextTier:0} kWh left in your current tier)."
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Approximates energy consumed across a set of readings using the
    /// trapezoidal rule on instantaneous power over time - i.e. real
    /// numerical integration of P(t) rather than a flat average.
    /// </summary>
    private static double EnergyFromReadings(List<Reading> orderedReadings)
    {
        if (orderedReadings.Count == 0) return 0;
        if (orderedReadings.Count == 1) return 0;

        double kwh = 0;
        for (int i = 1; i < orderedReadings.Count; i++)
        {
            var prev = orderedReadings[i - 1];
            var curr = orderedReadings[i];
            var hours = (curr.Timestamp - prev.Timestamp).TotalHours;
            if (hours <= 0 || hours > 6) continue; // skip gaps (e.g. Wi-Fi outage) rather than fabricating energy
            var avgWatts = (prev.Power + curr.Power) / 2.0;
            kwh += avgWatts / 1000.0 * hours;
        }
        return kwh;
    }

    private static List<ApplianceBreakdownItem> BuildDeviceBreakdown(List<Device> devices, List<Reading> windowReadings)
    {
        var perDeviceKwh = new Dictionary<int, double>();
        foreach (var device in devices)
        {
            var deviceReadings = windowReadings.Where(r => r.DeviceId == device.Id).OrderBy(r => r.Timestamp).ToList();
            perDeviceKwh[device.Id] = deviceReadings.Count > 1 ? EnergyFromReadings(deviceReadings) : device.TodayKwh;
        }

        var total = perDeviceKwh.Values.Sum();
        if (total <= 0)
        {
            return new List<ApplianceBreakdownItem>();
        }

        return devices
            .Select(d => new ApplianceBreakdownItem(d.Name, Math.Round(perDeviceKwh[d.Id] / total * 100.0, 1)))
            .Where(item => item.Percent > 0)
            .OrderByDescending(item => item.Percent)
            .ToList();
    }
}
