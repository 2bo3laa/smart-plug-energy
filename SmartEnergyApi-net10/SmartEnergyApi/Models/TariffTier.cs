namespace SmartEnergyApi.Models;

/// <summary>
/// One tier of a tiered electricity tariff (e.g. the Egyptian Electricity
/// Holding Company's stepped residential pricing). Used to turn kWh
/// consumption into an estimated cost (FR11); managed by an admin (FR13).
/// </summary>
public class TariffTier
{
    public int Id { get; set; }

    public double MinKwh { get; set; }
    public double? MaxKwh { get; set; } // null = open-ended top tier

    public double PricePerKwh { get; set; }
}
