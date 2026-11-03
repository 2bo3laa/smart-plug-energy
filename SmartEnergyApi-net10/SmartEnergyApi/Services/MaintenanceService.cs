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

/// <summary>Runs MaintenanceService shortly after start and then every hour while the app is awake.
/// (On hosts that put the app to sleep, the scheduled GitHub workflow calls /api/maintenance/run as well.)</summary>
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
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var (purged, backedUp) = await scope.ServiceProvider.GetRequiredService<MaintenanceService>().RunAsync();
                if (purged > 0 || backedUp > 0) _log.LogInformation("Maintenance: {Purged} account(s) wiped, {Backups} automatic backup(s) taken", purged, backedUp);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Maintenance run failed");
            }
            try { await Task.Delay(TimeSpan.FromHours(1), stop); } catch (OperationCanceledException) { return; }
        }
    }
}
