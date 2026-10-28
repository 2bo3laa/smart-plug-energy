using SmartEnergyApi.Models;

namespace SmartEnergyApi.Data;

public static class SeedData
{
    /// <summary>Seeds demo devices/schedules/alerts for a freshly-registered user so the app isn't empty.</summary>
    public static void SeedForUser(AppDbContext db, int userId)
    {
        var d1 = new Device
        {
            UserId = userId, Name = "Smart Plug 01", Room = "Living Room", RatedWatts = 450,
            IsOnline = true, IsOn = true,
            Voltage = 229.4, Current = 1.63, Power = 374.2, PowerFactor = 0.45,
            TodayKwh = 2.45, TodayCost = 3.68
        };
        var d2 = new Device
        {
            UserId = userId, Name = "Smart Plug 02", Room = "Bedroom", RatedWatts = 250,
            IsOnline = true, IsOn = true,
            Voltage = 221.8, Current = 0.92, Power = 198.5, PowerFactor = 0.52,
            TodayKwh = 1.10, TodayCost = 1.65
        };
        var d3 = new Device
        {
            UserId = userId, Name = "Smart Plug 03", Room = "Kitchen", RatedWatts = 1500,
            IsOnline = false, IsOn = false
        };
        db.Devices.AddRange(d1, d2, d3);
        db.SaveChanges();

        db.Schedules.AddRange(
            new Schedule { DeviceId = d1.Id, Label = "Morning ON", Time = "07:00", Days = "Weekdays", Type = "ON", IsActive = true },
            new Schedule { DeviceId = d1.Id, Label = "Night OFF", Time = "23:00", Days = "Everyday", Type = "OFF", IsActive = true },
            new Schedule { DeviceId = d1.Id, Label = "Afternoon OFF", Time = "14:00", Days = "Weekdays", Type = "OFF", IsActive = false },
            new Schedule { DeviceId = d1.Id, Label = "Weekend ON", Time = "09:00", Days = "Weekends", Type = "ON", IsActive = false }
        );

        db.Thresholds.Add(new Threshold { DeviceId = d1.Id, LimitValue = 1500, IsActive = true });

        db.Alerts.AddRange(
            new Alert { UserId = userId, DeviceId = d1.Id, Type = "power", Title = "High Power Usage", Message = "Power exceeded 1500W", Severity = "error" },
            new Alert { UserId = userId, DeviceId = d1.Id, Type = "voltage", Title = "Voltage Fluctuation", Message = "Voltage is outside the range", Severity = "warning" },
            new Alert { UserId = userId, DeviceId = d2.Id, Type = "device_off", Title = "Device Turned OFF", Message = "Smart Plug 02 was turned off", Severity = "info" },
            new Alert { UserId = userId, DeviceId = d1.Id, Type = "energy", Title = "High Energy Usage", Message = "Daily usage is higher than usual", Severity = "error" },
            new Alert { UserId = userId, DeviceId = d1.Id, Type = "device_on", Title = "Device Turned ON", Message = "Smart Plug 01 was turned on", Severity = "success" }
        );

        // A handful of historical readings so charts/statistics have real data to summarize immediately.
        var rng = new Random(userId);
        var now = DateTime.UtcNow;
        foreach (var device in new[] { d1, d2 })
        {
            double cumulative = 0;
            for (int hoursAgo = 47; hoursAgo >= 0; hoursAgo--)
            {
                var basePower = device.Id == d1.Id ? 350 : 190;
                var power = basePower + (rng.NextDouble() - 0.5) * 120;
                var energyThisHour = power / 1000.0; // kWh for a 1-hour bucket
                cumulative += energyThisHour;
                db.Readings.Add(new Reading
                {
                    DeviceId = device.Id,
                    Voltage = 220 + rng.NextDouble() * 10,
                    Current = Math.Round(power / 220.0, 2),
                    Power = Math.Round(power, 1),
                    Energy = Math.Round(cumulative, 3),
                    Frequency = 50,
                    PowerFactor = Math.Round(0.4 + rng.NextDouble() * 0.2, 2),
                    Timestamp = now.AddHours(-hoursAgo)
                });
            }
            device.CumulativeEnergyKwh = cumulative;
        }

        db.SaveChanges();
    }

    /// <summary>Default Egyptian-style tiered residential tariff, used until an admin edits it (FR11/FR13).</summary>
    public static void SeedDefaultTariffTiers(AppDbContext db)
    {
        if (db.TariffTiers.Any()) return;

        db.TariffTiers.AddRange(
            new TariffTier { MinKwh = 0, MaxKwh = 50, PricePerKwh = 0.68 },
            new TariffTier { MinKwh = 50, MaxKwh = 100, PricePerKwh = 0.78 },
            new TariffTier { MinKwh = 100, MaxKwh = 200, PricePerKwh = 0.95 },
            new TariffTier { MinKwh = 200, MaxKwh = 350, PricePerKwh = 1.55 },
            new TariffTier { MinKwh = 350, MaxKwh = 650, PricePerKwh = 1.95 },
            new TariffTier { MinKwh = 650, MaxKwh = 1000, PricePerKwh = 2.10 },
            new TariffTier { MinKwh = 1000, MaxKwh = null, PricePerKwh = 2.35 }
        );
        db.SaveChanges();
    }
}
