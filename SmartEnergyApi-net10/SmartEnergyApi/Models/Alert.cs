namespace SmartEnergyApi.Models;

public class Alert
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? DeviceId { get; set; }
    public Device? Device { get; set; }

    public string Type { get; set; } = string.Empty;      // power | voltage | device_on | device_off | energy
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";         // info | success | warning | error
    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
