namespace SmartEnergyApi.Models;

/// <summary>
/// A single measurement pushed by the ESP32 firmware from the PZEM-004T V3
/// (voltage, current, power, energy, frequency, power factor) - FR4/FR5/FR6.
/// This is the real data source for historical charts and cost estimation;
/// it replaces the randomly-generated numbers in the original prototype.
/// </summary>
public class Reading
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power { get; set; }          // Watts (instantaneous active power)
    public double Energy { get; set; }         // kWh, cumulative counter reported by the PZEM-004T V3
    public double Frequency { get; set; } = 50;
    public double PowerFactor { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
