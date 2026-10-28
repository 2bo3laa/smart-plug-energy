namespace SmartEnergyApi.Models;

public class Schedule
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string Label { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;   // "HH:mm"
    public string Days { get; set; } = string.Empty;   // "Weekdays" | "Weekends" | "Everyday" | ...
    public string Type { get; set; } = "ON";           // "ON" | "OFF"
    public bool IsActive { get; set; } = true;
}
