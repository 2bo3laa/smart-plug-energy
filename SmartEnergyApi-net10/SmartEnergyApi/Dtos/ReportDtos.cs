namespace SmartEnergyApi.Dtos;

public record ReportSettingsDto(bool Weekly, bool Monthly, string Email, bool MailConfigured, string Lang);
public record SaveReportSettingsRequest(bool Weekly, bool Monthly, string? Lang);
public record SendReportNowRequest(string? Period, string? Lang);
