namespace SmartEnergyApi.Dtos;

public record SignupRequest(string? Name, string? Email, string? Password);
public record LoginRequest(string? Email, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record ForgotPasswordRequest(string? Email);
public record ResetPasswordRequest(string? Email, string? Code, string? NewPassword);

public record PublicUserDto(int Id, string Name, string Email);
public record AuthResponse(string Token, PublicUserDto User);
