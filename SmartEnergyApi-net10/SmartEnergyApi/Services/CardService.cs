using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Prepaid electricity card: the user types in how much was loaded onto the card, the balance then
/// goes down as the plugs consume energy, priced with the stepped tariff. It also owns the two
/// "money" alerts: low/empty card balance and "you moved into a higher tariff tier".
///
/// Consumption is accounted for the moment a reading arrives (RecordConsumptionAsync) and kept in running
/// counters (User.MonthKwh, Device.CardKwh/CardCostEgp), so reading the balance never has to scan old readings.
/// </summary>
public class CardService
{
    private readonly AppDbContext _db;
    private readonly TariffService _tariff;

    public CardService(AppDbContext db, TariffService tariff)
    {
        _db = db;
        _tariff = tariff;
    }

    private record Sample(int DeviceId, DateTime Ts, double Power);

    /// <summary>Trapezoid integration of power (W) over time -> kWh, one device at a time. Gaps over 6 h are skipped.</summary>
    private static double Integrate(IEnumerable<Sample> samples)
    {
        double kwh = 0;
        foreach (var g in samples.GroupBy(s => s.DeviceId))
        {
            var list = g.OrderBy(s => s.Ts).ToList();
            for (int i = 1; i < list.Count; i++)
            {
                var hours = (list[i].Ts - list[i - 1].Ts).TotalHours;
                if (hours <= 0 || hours > 6) continue;
                kwh += (list[i - 1].Power + list[i].Power) / 2.0 / 1000.0 * hours;
            }
        }
        return kwh;
    }

    private static double IntegrateDevice(IEnumerable<Sample> samples, int deviceId) =>
        Integrate(samples.Where(s => s.DeviceId == deviceId));

    // ------------------------------------------------------------------------------------------
    // Called by the readings endpoint for every reading (changes are saved by the caller).
    // ------------------------------------------------------------------------------------------
    public async Task RecordConsumptionAsync(Device device, double kwh)
    {
        if (kwh <= 0 || double.IsNaN(kwh) || double.IsInfinity(kwh)) return;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == device.UserId);
        if (user == null) return;

        var now = DateTime.UtcNow;
        var key = now.Year * 100 + now.Month;
        if (user.MonthKey != key)
        {
            // New month (or first run after the upgrade): start from what the readings already show for this month.
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            user.MonthKwh = await MonthEnergyAsync(user.Id, monthStart);
            user.MonthKey = key;
            var start = await _tariff.GetTierPosAsync(user.MonthKwh);
            user.TierMonth = key;
            user.TierIndex = start.Index;   // remember the starting tier, no alert
        }

        var before = await _tariff.GetTierPosAsync(user.MonthKwh);
        var cost = kwh * before.Price;
        user.MonthKwh += kwh;
        var after = await _tariff.GetTierPosAsync(user.MonthKwh);

        if (after.Index > user.TierIndex)
        {
            _db.Alerts.Add(new Alert
            {
                UserId = user.Id,
                Type = "tier",
                Severity = "warning",
                Title = "Moved to a higher tariff tier",
                Message = $"You have used {user.MonthKwh:0} kWh this month and moved into tier {after.Index + 1} ({after.Price:0.00} EGP/kWh)."
            });
        }
        user.TierIndex = after.Index;
        user.TierMonth = key;

        // The card only counts once the user has entered a charge (and old cards were rebuilt, see BackfillAsync).
        if (user.CardBaseAt == null || !user.CardCountersReady) return;
        device.CardKwh += kwh;
        device.CardCostEgp += cost;
    }

    private async Task<double> MonthEnergyAsync(int userId, DateTime monthStart)
    {
        var ids = await _db.Devices.Where(d => d.UserId == userId).Select(d => d.Id).ToListAsync();
        var samples = await _db.Readings
            .Where(r => ids.Contains(r.DeviceId) && r.Timestamp >= monthStart)
            .Select(r => new Sample(r.DeviceId, r.Timestamp, r.Power))
            .ToListAsync();
        return Integrate(samples);
    }

    // ------------------------------------------------------------------------------------------
    // Card summary (cheap: reads only the running counters)
    // ------------------------------------------------------------------------------------------
    public async Task<CardDto> GetSummaryAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        var devices = await _db.Devices.Where(d => d.UserId == userId).OrderBy(d => d.Id).ToListAsync();
        if (user == null || user.CardBaseAt == null)
        {
            return new CardDto(false, 0, 0, null, 0, 0, false, false,
                devices.Select(d => new CardDeviceUsage(d.Id, d.Name, d.Room, d.Type, 0, 0, 0)).ToList());
        }

        if (!user.CardCountersReady) await BackfillAsync(user, devices);

        var since = DateTime.SpecifyKind(user.CardBaseAt.Value, DateTimeKind.Utc);
        var spent = devices.Sum(d => d.CardCostEgp);
        var totalKwh = devices.Sum(d => d.CardKwh);
        var balance = Math.Max(0, user.CardBalanceBase - spent);
        var empty = balance <= 0.005;
        var low = !empty && balance <= Math.Max(10, user.CardBalanceBase * 0.1);

        var rows = devices
            .Select(d => new CardDeviceUsage(d.Id, d.Name, d.Room, d.Type,
                Math.Round(d.CardKwh, 3), Math.Round(d.CardCostEgp, 2),
                spent > 0 ? Math.Round(d.CardCostEgp / spent * 100, 1) : 0))
            .OrderByDescending(r => r.Cost)
            .ToList();

        return new CardDto(true, Math.Round(balance, 2), Math.Round(user.CardBalanceBase, 2), since,
            Math.Round(spent, 2), Math.Round(totalKwh, 3), low, empty, rows);
    }

    /// <summary>
    /// One-off: a card that was created before the running counters existed is rebuilt once from the stored
    /// readings (the old way of counting), then it switches to the cheap counters for good.
    /// </summary>
    private async Task BackfillAsync(User user, List<Device> devices)
    {
        var since = DateTime.SpecifyKind(user.CardBaseAt!.Value, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(since.Year, since.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = devices.Select(d => d.Id).ToList();

        var samples = await _db.Readings
            .Where(r => ids.Contains(r.DeviceId) && r.Timestamp >= firstMonth)
            .Select(r => new Sample(r.DeviceId, r.Timestamp, r.Power))
            .ToListAsync();

        foreach (var d in devices) { d.CardKwh = 0; d.CardCostEgp = 0; }

        // Tiers restart every month, so price each calendar month separately.
        var monthStart = firstMonth;
        for (int guard = 0; monthStart <= now && guard < 36; guard++)
        {
            var nextMonth = monthStart.AddMonths(1);
            var segStart = since > monthStart ? since : monthStart;
            var segEnd = now < nextMonth ? now : nextMonth;
            if (segEnd > segStart)
            {
                var before = Integrate(samples.Where(s => s.Ts >= monthStart && s.Ts <= segStart));
                var inSeg = samples.Where(s => s.Ts >= segStart && s.Ts <= segEnd).ToList();
                var segTotal = Integrate(inSeg);
                if (segTotal > 0)
                {
                    var segCost = await _tariff.EstimateCostAsync(before + segTotal) - await _tariff.EstimateCostAsync(before);
                    foreach (var d in devices)
                    {
                        var k = IntegrateDevice(inSeg, d.Id);
                        d.CardKwh += k;
                        d.CardCostEgp += segCost * (k / segTotal);
                    }
                }
            }
            monthStart = nextMonth;
        }

        var curStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        user.MonthKwh = Integrate(samples.Where(s => s.Ts >= curStart));
        user.MonthKey = now.Year * 100 + now.Month;
        user.CardCountersReady = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>Adds money to the card (keeps what is left) and restarts the counting from now.</summary>
    public async Task RechargeAsync(int userId, double amount)
    {
        var current = await GetSummaryAsync(userId);
        await SetBalanceAsync(userId, (current.HasCard ? current.Balance : 0) + amount);
    }

    /// <summary>Sets the balance to exactly this value from now on.</summary>
    public async Task SetBalanceAsync(int userId, double balance)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.CardBalanceBase = Math.Round(balance, 2);
        user.CardBaseAt = DateTime.UtcNow;
        user.CardAlertLevel = 0;
        user.CardCountersReady = true;
        var devices = await _db.Devices.Where(d => d.UserId == userId).ToListAsync();
        foreach (var d in devices) { d.CardKwh = 0; d.CardCostEgp = 0; }
        await _db.SaveChangesAsync();
    }

    /// <summary>Raises a warning when the card gets low and an error when it runs out (once each per recharge).</summary>
    public async Task CheckBalanceAlertsAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.CardBaseAt == null) return;
        var s = await GetSummaryAsync(userId);
        var level = s.Empty ? 2 : s.Low ? 1 : 0;
        if (level <= user.CardAlertLevel) return;
        user.CardAlertLevel = level;
        _db.Alerts.Add(new Alert
        {
            UserId = userId,
            Type = "card",
            Severity = level == 2 ? "error" : "warning",
            Title = level == 2 ? "Card balance is empty" : "Card balance is low",
            Message = level == 2
                ? "Your electricity card balance has run out. Recharge it to avoid a power cut."
                : $"Only {s.Balance:0.00} EGP is left on your electricity card."
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Tier crossings are now detected the moment a reading arrives (RecordConsumptionAsync), so this is a no-op;
    /// it stays only so older callers keep compiling.
    /// </summary>
    public Task CheckTierCrossingAsync(int userId) => Task.CompletedTask;
}
