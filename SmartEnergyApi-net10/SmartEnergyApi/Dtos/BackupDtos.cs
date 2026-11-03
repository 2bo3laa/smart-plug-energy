namespace SmartEnergyApi.Dtos;

public record BackupDto(int Id, string Kind, DateTime CreatedAt, int Devices, int Schedules, int SizeBytes);
public record ImportBackupRequest(string? Text);
public record EmailBackupRequest(string? Lang);
