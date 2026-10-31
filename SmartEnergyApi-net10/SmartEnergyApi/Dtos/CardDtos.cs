namespace SmartEnergyApi.Dtos;

public record CardDeviceUsage(int DeviceId, string Name, string Room, string? Type, double Kwh, double Cost, double Percent);

/// <summary>Prepaid-card state: what was loaded, what is left, and roughly what each device has used.</summary>
public record CardDto(
    bool HasCard,
    double Balance,
    double Charged,
    DateTime? Since,
    double Spent,
    double TotalKwh,
    bool Low,
    bool Empty,
    List<CardDeviceUsage> Devices
);

public record CardAmountRequest(double Amount);
public record GoogleTokenRequest(string? IdToken);
