using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;

namespace SmartEnergyApi.Services;

/// <summary>Permanent removal of an account and everything that belongs to it.</summary>
public class AccountService
{
    /// <summary>Days a deleted account can still be restored before it is wiped.</summary>
    public const int DeletionGraceDays = 30;

    private readonly AppDbContext _db;
    public AccountService(AppDbContext db) { _db = db; }

    /// <summary>Wipes the user and all of their data (readings, schedules, power limits, alerts, devices, backups, reset codes).</summary>
    public async Task PurgeAsync(int userId)
    {
        // Explicit deletes (not only the cascade rules) so it also works on databases that were upgraded in place.
        var myDevices = _db.Devices.Where(d => d.UserId == userId).Select(d => d.Id);
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.Readings.Where(r => myDevices.Contains(r.DeviceId)).ExecuteDeleteAsync();
        await _db.Schedules.Where(s => myDevices.Contains(s.DeviceId)).ExecuteDeleteAsync();
        await _db.Thresholds.Where(t => myDevices.Contains(t.DeviceId)).ExecuteDeleteAsync();
        await _db.Alerts.Where(a => a.UserId == userId).ExecuteDeleteAsync();
        await _db.Devices.Where(d => d.UserId == userId).ExecuteDeleteAsync();
        await _db.Backups.Where(b => b.UserId == userId).ExecuteDeleteAsync();
        await _db.PasswordResets.Where(p => p.UserId == userId).ExecuteDeleteAsync();
        await _db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        await tx.CommitAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Wipes every account whose 30 days are over. Returns how many were removed.</summary>
    public async Task<int> PurgeExpiredAsync()
    {
        var now = DateTime.UtcNow;
        var ids = await _db.Users.Where(u => u.PurgeAt != null && u.PurgeAt <= now).Select(u => u.Id).ToListAsync();
        foreach (var id in ids) await PurgeAsync(id);
        return ids.Count;
    }
}
