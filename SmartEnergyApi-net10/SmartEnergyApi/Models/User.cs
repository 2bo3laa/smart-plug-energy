using System.ComponentModel.DataAnnotations;

namespace SmartEnergyApi.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Household user or admin. Drives access to tariff-tier management (FR13).</summary>
    public string Role { get; set; } = "user";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Prepaid electricity card ----
    // Balance now = CardBalanceBase - cost of the energy used since CardBaseAt.
    public double CardBalanceBase { get; set; }
    public DateTime? CardBaseAt { get; set; }
    /// <summary>0 = fine, 1 = low-balance alert sent, 2 = empty alert sent (reset on every recharge).</summary>
    public int CardAlertLevel { get; set; }

    // ---- Tariff tier tracking (for the "you moved to a higher tier" alert) ----
    /// <summary>yyyymm of the month TierIndex refers to (0 = not initialised).</summary>
    public int TierMonth { get; set; }
    /// <summary>Running consumption of the current month (kWh) and the yyyymm it belongs to. Updated on every reading.</summary>
    public int MonthKey { get; set; }
    public double MonthKwh { get; set; }
    /// <summary>False for cards created before running counters existed: they are rebuilt once from the stored readings.</summary>
    public bool CardCountersReady { get; set; }
    public int TierIndex { get; set; }

    // ---- Linked Google account ----
    [MaxLength(64)] public string? GoogleId { get; set; }
    [MaxLength(180)] public string? GoogleEmail { get; set; }

    public List<Device> Devices { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
}
