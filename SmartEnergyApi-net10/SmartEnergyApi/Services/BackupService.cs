using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Creates, validates and restores per-user backups. A backup holds the user's setup only
/// (devices, schedules, power limits, report settings), never the readings history or the prepaid card balance.
/// </summary>
public class BackupService
{
    public const int MaxPerUser = 5;
    public const int FormatVersion = 1;
    private const int MaxTextBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly AppDbContext _db;

    public BackupService(AppDbContext db) { _db = db; }

    // ---- file format ----
    public class BackupFile
    {
        public string App { get; set; } = "smart-plug-energy";
        public int Version { get; set; } = FormatVersion;
        public DateTime CreatedAt { get; set; }
        public SettingsPart Settings { get; set; } = new();
        public List<DevicePart> Devices { get; set; } = new();
    }
    public class SettingsPart
    {
        public bool ReportWeekly { get; set; }
        public bool ReportMonthly { get; set; }
        public string? ReportLang { get; set; }
    }
    public class DevicePart
    {
        public string DeviceKey { get; set; } = "";
        public string Name { get; set; } = "";
        public string Room { get; set; } = "";
        public string? Type { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public double? RatedWatts { get; set; }
        public bool IsSimulated { get; set; }
        public double? ThresholdWatts { get; set; }
        public bool ThresholdActive { get; set; } = true;
        public List<SchedulePart> Schedules { get; set; } = new();
    }
    public class SchedulePart
    {
        public string Label { get; set; } = "";
        public string Time { get; set; } = "";
        public string Days { get; set; } = "";
        public string Type { get; set; } = "ON";
        public bool IsActive { get; set; } = true;
    }

    // ---- create ----
    /// <summary>Takes a snapshot of the user's setup and stores it. Older backups beyond MaxPerUser are dropped.</summary>
    public async Task<Backup> CreateAsync(User user, string kind)
    {
        var devices = await _db.Devices.Where(d => d.UserId == user.Id)
            .Include(d => d.Schedules).Include(d => d.Thresholds)
            .AsNoTracking().OrderBy(d => d.Id).ToListAsync();

        var file = new BackupFile
        {
            CreatedAt = DateTime.UtcNow,
            Settings = new SettingsPart { ReportWeekly = user.ReportWeekly, ReportMonthly = user.ReportMonthly, ReportLang = user.ReportLang },
            Devices = devices.Select(d =>
            {
                var th = d.Thresholds.OrderByDescending(t => t.Id).FirstOrDefault();
                return new DevicePart
                {
                    DeviceKey = d.DeviceKey, Name = d.Name, Room = d.Room, Type = d.Type, Brand = d.Brand, Model = d.Model,
                    RatedWatts = d.RatedWatts, IsSimulated = d.IsSimulated,
                    ThresholdWatts = th?.LimitValue, ThresholdActive = th?.IsActive ?? true,
                    Schedules = d.Schedules.OrderBy(s => s.Id).Select(s => new SchedulePart
                        { Label = s.Label, Time = s.Time, Days = s.Days, Type = s.Type, IsActive = s.IsActive }).ToList()
                };
            }).ToList()
        };
        return await StoreAsync(user.Id, kind, file);
    }

    private async Task<Backup> StoreAsync(int userId, string kind, BackupFile file)
    {
        var text = JsonSerializer.Serialize(file, Json);
        var backup = new Backup
        {
            UserId = userId, Kind = kind, CreatedAt = DateTime.UtcNow, Data = text,
            DeviceCount = file.Devices.Count, ScheduleCount = file.Devices.Sum(d => d.Schedules.Count),
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(text)
        };
        _db.Backups.Add(backup);
        await _db.SaveChangesAsync();
        await PruneAsync(userId);
        return backup;
    }

    /// <summary>Keeps only the newest MaxPerUser backups of a user.</summary>
    public async Task PruneAsync(int userId)
    {
        var extra = await _db.Backups.Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Skip(MaxPerUser).Select(b => b.Id).ToListAsync();
        if (extra.Count > 0)
            await _db.Backups.Where(b => extra.Contains(b.Id)).ExecuteDeleteAsync();
    }

    // ---- import ----
    /// <summary>Parses and checks backup text (as produced by "copy backup text" / the e-mailed file). Throws FormatException with a short message.</summary>
    public static BackupFile Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Paste the backup text first");
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxTextBytes) throw new FormatException("The backup text is too large");
        BackupFile? file;
        try { file = JsonSerializer.Deserialize<BackupFile>(text.Trim().TrimStart('\uFEFF'), Json); }
        catch (JsonException) { throw new FormatException("This is not a valid backup text"); }
        if (file == null || file.App != "smart-plug-energy") throw new FormatException("This is not a Smart Plug backup");
        if (file.Version != FormatVersion) throw new FormatException("This backup version is not supported");
        if (file.Devices == null || file.Settings == null) throw new FormatException("This is not a valid backup text");
        if (file.Devices.Count > 100) throw new FormatException("The backup has too many devices");
        foreach (var d in file.Devices)
        {
            if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 80 || (d.Room ?? "").Length > 80)
                throw new FormatException("This is not a valid backup text");
            if (d.Schedules == null) d.Schedules = new();
            if (d.Schedules.Count > 100) throw new FormatException("The backup has too many schedules");
            foreach (var s in d.Schedules)
                if (s.Type != "ON" && s.Type != "OFF") throw new FormatException("This is not a valid backup text");
        }
        return file;
    }

    public async Task<Backup> ImportAsync(int userId, string? text)
    {
        var file = Parse(text);   // re-serialised below, so only known fields are stored
        return await StoreAsync(userId, "import", file);
    }

    // ---- restore ----
    /// <summary>
    /// Puts the user's setup back to what the backup holds. Devices that are in the backup are updated (their readings stay)
    /// or created again, devices that are not in it are removed. A safety copy of the current setup is taken first.
    /// </summary>
    public async Task RestoreAsync(User user, Backup backup)
    {
        var file = Parse(backup.Data);
        await CreateAsync(user, "before_restore");

        user.ReportWeekly = file.Settings.ReportWeekly;
        user.ReportMonthly = file.Settings.ReportMonthly;
        user.ReportLang = file.Settings.ReportLang == "ar" ? "ar" : "en";

        var current = await _db.Devices.Where(d => d.UserId == user.Id)
            .Include(d => d.Schedules).Include(d => d.Thresholds).ToListAsync();

        var keep = new HashSet<int>();
        foreach (var p in file.Devices)
        {
            var dev = current.FirstOrDefault(d => d.DeviceKey == p.DeviceKey);
            if (dev == null)
            {
                // DeviceKey is unique across all users: keep the saved one only if nobody else holds it now.
                var key = string.IsNullOrWhiteSpace(p.DeviceKey) || await _db.Devices.AnyAsync(d => d.DeviceKey == p.DeviceKey)
                    ? Guid.NewGuid().ToString("N") : p.DeviceKey;
                dev = new Device { UserId = user.Id, DeviceKey = key, IsSimulated = p.IsSimulated, IsOn = false };
                _db.Devices.Add(dev);
            }
            dev.Name = p.Name.Trim();
            dev.Room = string.IsNullOrWhiteSpace(p.Room) ? "—" : p.Room.Trim();
            dev.Type = p.Type; dev.Brand = p.Brand; dev.Model = p.Model; dev.RatedWatts = p.RatedWatts;

            _db.Schedules.RemoveRange(dev.Schedules);
            dev.Schedules = p.Schedules.Select(s => new Schedule
                { Label = s.Label ?? "", Time = s.Time ?? "", Days = s.Days ?? "", Type = s.Type, IsActive = s.IsActive }).ToList();

            _db.Thresholds.RemoveRange(dev.Thresholds);
            dev.Thresholds = p.ThresholdWatts is double w
                ? new List<Threshold> { new() { LimitValue = w, IsActive = p.ThresholdActive } }
                : new List<Threshold>();
            if (dev.Id != 0) keep.Add(dev.Id);
        }

        _db.Devices.RemoveRange(current.Where(d => !keep.Contains(d.Id)));
        await _db.SaveChangesAsync();
    }

    // ---- e-mail ----
    public static string FileName(Backup b) => $"smart-plug-backup-{b.CreatedAt:yyyyMMdd-HHmm}.json";
}
