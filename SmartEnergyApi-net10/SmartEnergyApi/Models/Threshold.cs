namespace SmartEnergyApi.Models;

/// <summary>Per-device consumption threshold; crossing it raises an Alert (FR9/FR10).</summary>
public class Threshold
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>Power limit in Watts that triggers an alert when a Reading exceeds it.</summary>
    public double LimitValue { get; set; }

    public bool IsActive { get; set; } = true;
}
