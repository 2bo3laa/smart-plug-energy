namespace SmartEnergyApi.Dtos;

public record ScheduleDto(int Id, int DeviceId, string Label, string Time, string Days, string Type, bool IsActive);
public record CreateScheduleRequest(string? Label, string? Time, string? Days, string? Type);
public record UpdateScheduleRequest(string? Label, string? Time, string? Days, string? Type);

public record AlertDto(int Id, int? DeviceId, string Type, string Title, string Message, string Severity, bool IsRead, DateTime CreatedAt);
public record AlertListResponse(List<AlertDto> Alerts, int UnreadCount);

public record ApplianceBreakdownItem(string Label, double Percent);
public record StatisticsResponse(
    string Range,
    double EnergyToday,
    double TodayCost,
    double ChangeVsYesterday,
    List<double> Chart,
    double TotalEnergyMonth,
    double TotalCostMonth,
    List<ApplianceBreakdownItem> ApplianceBreakdown,
    ForecastDto? Forecast = null
);

/// <summary>Month-end projection based on the pace so far, and where it lands in the tiered tariff.</summary>
public record ForecastDto(
    double ProjectedEnergyMonth,
    double ProjectedCostMonth,
    double CurrentPricePerKwh,
    double? NextPricePerKwh,
    double? KwhUntilNextTier,
    bool WillCrossNextTier
);

public record ProfileStatsDto(int Devices, int ActiveSchedules);
public record ProfileResponse(PublicUserDto User, ProfileStatsDto Stats);
public record UpdateProfileRequest(string? Name);

public record TariffTierDto(int Id, double MinKwh, double? MaxKwh, double PricePerKwh);
public record UpsertTariffTierRequest(double MinKwh, double? MaxKwh, double PricePerKwh);
