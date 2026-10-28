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

    public List<Device> Devices { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
}
