using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Built-in "virtual plug": devices added from the app that have no real ESP32 behind them behave like real ones.
/// Whenever the app asks for data, the missing readings since the device was last seen are generated here and run
/// through the normal reading pipeline, so power, energy, card balance, tiers and alerts all work without hardware.
/// A device stops being simulated as soon as a real reading arrives from hardware.
/// Turn it off with  "Demo": { "SimulateDevices": false }  in secrets.local.json.
/// </summary>
public class SimulationService
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    private readonly AppDbContext _db;
    private readonly ReadingService _readings;

    public bool Enabled { get; }

    public SimulationService(AppDbContext db, ReadingService readings, IConfiguration config)
    {
        _db = db;
        _readings = readings;
        Enabled = !string.Equals(config["Demo:SimulateDevices"], "false", StringComparison.OrdinalIgnoreCase);
    }

    public async Task TickAsync(int userId)
    {
        if (!Enabled) return;

        var gate = Locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var devices = await _db.Devices.Include(d => d.Thresholds)
                .Where(d => d.UserId == userId && d.IsSimulated)
                .ToListAsync();
            if (devices.Count == 0) return;

            var now = DateTime.UtcNow;
            var touched = false;
            foreach (var d in devices)
            {
                var last = d.LastSeenAt ?? d.CreatedAt;
                var gap = now - last;
                if (gap.TotalSeconds < 4) continue;
                if (gap > TimeSpan.FromHours(24))
                {
                    last = now.AddHours(-24);
                    gap = now - last;
                }

                // One reading every ~5 s while the app is open; coarser steps to fill a long gap (max 60 readings).
                var steps = (int)Math.Min(60, Math.Max(1, Math.Ceiling(gap.TotalSeconds / 5.0)));
                var step = TimeSpan.FromTicks(gap.Ticks / steps);

                if (d.PendingRelayCommand != null) d.PendingRelayCommand = null;   // a virtual plug "obeys" instantly

                Reading? prev = null;
                for (int i = 1; i <= steps; i++)
                {
                    var ts = i == steps ? now : last + TimeSpan.FromTicks(step.Ticks * i);
                    prev = await _readings.ApplyReadingAsync(d, MakeReading(d, step), ts, prev, batch: true);
                }
                touched = true;
            }

            if (touched)
            {
                await _db.SaveChangesAsync();
                await _readings.AfterIngestAsync(userId);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static IngestReadingRequest MakeReading(Device d, TimeSpan step)
    {
        var rnd = Random.Shared;
        var voltage = 220 + (rnd.NextDouble() * 4 - 2);
        const double pf = 0.95;
        var power = d.IsOn ? BasePower(d) * (0.92 + 0.16 * rnd.NextDouble()) : 0;
        var current = power > 0 ? power / (voltage * pf) : 0;
        var energy = d.CumulativeEnergyKwh + power / 1000.0 * step.TotalHours;
        return new IngestReadingRequest(
            Math.Round(voltage, 1), Math.Round(current, 2), Math.Round(power, 1),
            energy, 50, power > 0 ? pf : 0, null);
    }

    private static double BasePower(Device d)
    {
        if (d.RatedWatts is double r && r > 0) return r;
        var t = (d.Type ?? "").ToLowerInvariant();
        if (t.Contains("refrig")) return 150;
        if (t.Contains("freez")) return 120;
        if (t.Contains("wash")) return 500;
        if (t.Contains("air") || t.Contains("cond")) return 1200;
        if (t.Contains("tv")) return 100;
        if (t.Contains("blend")) return 400;
        if (t.Contains("micro")) return 1000;
        if (t.Contains("heater")) return 1500;
        if (t.Contains("dish")) return 1200;
        if (t.Contains("fan")) return 60;
        if (t.Contains("iron")) return 1000;
        if (t.Contains("vacuum")) return 800;
        if (t.Contains("kettle")) return 1500;
        if (t.Contains("toast")) return 800;
        if (t.Contains("comput")) return 200;
        return 100;
    }
}
