namespace SmartEnergyApi.Dtos;

public record SignupRequest(string? Name, string? Email, string? Password);
public record LoginRequest(string? Email, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record ForgotPasswordRequest(string? Email);
public record ResetPasswordRequest(string? Email, string? Code, string? NewPassword);

public record PublicUserDto(int Id, string Name, string Email);
public record AuthResponse(string Token, PublicUserDto User);

/// <summary>Delete (schedule deletion of) the signed-in account: the user must retype the e-mail and the password.</summary>
public record DeleteAccountRequest(string? Email, string? Password);
/// <summary>Restore a deleted account within 30 days: with the password, or (Google accounts) with a Google id token.</summary>
public record RestoreAccountRequest(string? Email, string? Password, string? IdToken);
