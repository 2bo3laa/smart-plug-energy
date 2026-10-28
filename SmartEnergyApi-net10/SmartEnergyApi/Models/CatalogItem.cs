namespace SmartEnergyApi.Models;

/// <summary>
/// One row of the appliance catalogue: a brand + model/size class of a given type with its rated power.
/// Source = "typical" (class average, not from a datasheet) or "manufacturer" (taken from the maker's spec/nameplate).
/// </summary>
public class CatalogItem
{
    public int Id { get; set; }
    public string TypeKey { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double RatedWatts { get; set; }
    public string Source { get; set; } = "typical";
}
