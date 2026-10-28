namespace SmartEnergyApi.Models;

/// <summary>Kind of appliance a smart plug can be attached to (fridge, washer, AC, ...).</summary>
public class ApplianceType
{
    public int Id { get; set; }
    /// <summary>Stable machine key, e.g. "refrigerator". The Flutter app maps it to an icon.</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Fallback rated power when no brand/model is chosen.</summary>
    public double DefaultWatts { get; set; }
    public int SortOrder { get; set; }
}
