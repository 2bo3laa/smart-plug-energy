using System.ComponentModel.DataAnnotations;

namespace SmartEnergyApi.Models;

/// <summary>
/// One ESP32 + PZEM-004T V3 + relay sensing-and-actuation node.
/// The live fields below (Voltage/Current/Power/...) always mirror the most
/// recent Reading so existing screens that expect an "instant snapshot" keep
/// working, while the Readings table holds the real time-series history.
/// </summary>
public class Device
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Room { get; set; } = string.Empty;

    /// <summary>Unique key the ESP32 firmware sends (header X-Device-Key) when pushing readings/commands.</summary>
    public string DeviceKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ApplianceType.Key (refrigerator, air_conditioner, ...). Null = not specified.</summary>
    public string? Type { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }

    /// <summary>Nameplate power of the connected appliance in watts (optional, entered by the user).</summary>
    public double? RatedWatts { get; set; }

    /// <summary>Last time the ESP32 pushed a reading or polled for a command. Null = never connected.
    /// Online status is derived from this (see Device:OfflineAfterSeconds) instead of a flag that never resets.</summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>kWh and EGP this device used since the user's last card charge (running counters, reset on every charge).</summary>
    public double CardKwh { get; set; }
    public double CardCostEgp { get; set; }

    public bool IsOnline { get; set; } = true;
    public bool IsOn { get; set; } = false;

    // Latest snapshot (also duplicated into Readings for history/analysis)
    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power { get; set; }
    public double PowerFactor { get; set; }
    public double Frequency { get; set; } = 50;

    public double TodayKwh { get; set; }
    public double TodayCost { get; set; }

    /// <summary>Cumulative energy counter as reported by the PZEM-004T V3 (kWh), used to derive TodayKwh.</summary>
    public double CumulativeEnergyKwh { get; set; }

    /// <summary>Pending relay command the ESP32 should apply next time it polls (FR16/FR17). Null = nothing pending.</summary>
    public bool? PendingRelayCommand { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Schedule> Schedules { get; set; } = new();
    public List<Alert> Alerts { get; set; } = new();
    public List<Reading> Readings { get; set; } = new();
    public List<Threshold> Thresholds { get; set; } = new();
}
