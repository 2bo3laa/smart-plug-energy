namespace SmartEnergyApi.Dtos;

public record DeviceDto(
    int Id,
    string Name,
    string Room,
    bool IsOnline,
    bool IsOn,
    double Voltage,
    double Current,
    double Power,
    double PowerFactor,
    double TodayKwh,
    double TodayCost,
    double? RatedWatts = null,
    string? Type = null,
    string? Brand = null,
    string? Model = null
);

public record DeviceListResponse(List<DeviceDto> Devices, int Total, int Online, int Offline);
public record CreateDeviceRequest(string? Name, string? Room, double? RatedWatts = null, string? Type = null, string? Brand = null, string? Model = null);
public record UpdateDeviceRequest(string? Name, string? Room, double? RatedWatts = null, string? Type = null, string? Brand = null, string? Model = null);

public record ScanResultDto(string TempId, string Name, string Signal);
public record ScanResponse(List<ScanResultDto> Found);

/// <summary>Payload the ESP32 firmware pushes on each measurement cycle (FR5).</summary>
public record IngestReadingRequest(double Voltage, double Current, double Power, double Energy, double Frequency, double PowerFactor);

public record ReadingDto(long Id, double Voltage, double Current, double Power, double Energy, double Frequency, double PowerFactor, DateTime Timestamp);

/// <summary>What the ESP32 polls to learn whether it should switch the relay (FR17).</summary>
public record RelayCommandDto(bool? Command);

public record ThresholdDto(int Id, int DeviceId, double LimitValue, bool IsActive);
public record UpsertThresholdRequest(double LimitValue, bool? IsActive);
