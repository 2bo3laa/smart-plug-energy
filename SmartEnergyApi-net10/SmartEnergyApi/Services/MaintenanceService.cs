using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;

namespace SmartEnergyApi.Services;

/// <summary>The two periodic jobs: wipe accounts whose 30-day restore window is over, and take the weekly automatic backups.</summary>
public class MaintenanceService
{
    private readonly AppDbContext _db;
    private readonly AccountService _accounts;
    private readonly BackupService _backups;

    public MaintenanceService(AppDbContext db, AccountService accounts, BackupService backups)
    {
        _db = db;
        _accounts = accounts;
        _backups = backups;
    }

    public async Task<(int purged, int backedUp)> RunAsync()
    {
        var purged = await _accounts.PurgeExpiredAsync();

        // Weekly automatic backup: every active user that has devices and no automatic backup in the last 7 days.
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var recent = await _db.Backups.Where(b => b.Kind == "auto" && b.CreatedAt > cutoff).Select(b => b.UserId).Distinct().ToListAsync();
        var due = await _db.Users
            .Where(u => u.DeletedAt == null && !recent.Contains(u.Id) && _db.Devices.Any(d => d.UserId == u.Id))
            .ToListAsync();
        var backedUp = 0;
        foreach (var u in due)
        {
            try { await _backups.CreateAsync(u, "auto"); backedUp++; }
            catch { /* one user's failure must not stop the others */ }
        }
        return (purged, backedUp);
    }
}

/// <summary>
/// Background loop on the server. Every 5 minutes it advances the virtual plugs (devices without real hardware) of every active user,
/// so readings, the card balance, alerts and phone notifications keep coming even when the app is closed.
/// Once an hour it also runs MaintenanceService. (On hosts that put the app to sleep, the scheduled GitHub workflow calls
/// /api/maintenance/run as well.) Real ESP32 plugs need none of this: they post their readings to the server themselves.
/// </summary>
public class MaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MaintenanceWorker> _log;

    public MaintenanceWorker(IServiceScopeFactory scopes, ILogger<MaintenanceWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stop); } catch (OperationCanceledException) { return; }
        var lastMaintenance = DateTime.MinValue;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var sp = scope.ServiceProvider;

                var db = sp.GetRequiredService<AppDbContext>();
                var sim = sp.GetRequiredService<SimulationService>();
                if (sim.Enabled)
                {
                    var ids = await db.Users
                        .Where(u => u.DeletedAt == null && db.Devices.Any(d => d.UserId == u.Id && d.IsSimulated))
                        .Select(u => u.Id).ToListAsync(stop);
                    foreach (var id in ids)
                    {
                        try { await sim.TickAsync(id); }
                        catch (Exception ex) { _log.LogWarning(ex, "Simulation tick failed for user {UserId}", id); }
                    }
                }

                if (DateTime.UtcNow - lastMaintenance >= TimeSpan.FromHours(1))
                {
                    lastMaintenance = DateTime.UtcNow;
                    var (purged, backedUp) = await sp.GetRequiredService<MaintenanceService>().RunAsync();
                    if (purged > 0 || backedUp > 0) _log.LogInformation("Maintenance: {Purged} account(s) wiped, {Backups} automatic backup(s) taken", purged, backedUp);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Background run failed");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(5), stop); } catch (OperationCanceledException) { return; }
        }
    }
}
