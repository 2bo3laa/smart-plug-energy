using System.ComponentModel.DataAnnotations;

namespace SmartEnergyApi.Models;

/// <summary>
/// A saved copy of one user's setup (devices, schedules, power limits, report settings) as JSON text.
/// Readings (the energy history) are not part of a backup: they are large and keep growing.
/// At most <see cref="Services.BackupService.MaxPerUser"/> backups are kept per user (the oldest is dropped).
/// </summary>
public class Backup
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>manual | auto | import | before_restore</summary>
    [Required, MaxLength(20)]
    public string Kind { get; set; } = "manual";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The backup itself: a JSON document (see BackupService.BackupFile).</summary>
    [Required]
    public string Data { get; set; } = string.Empty;

    public int DeviceCount { get; set; }
    public int ScheduleCount { get; set; }
    public int SizeBytes { get; set; }
}
