namespace SmartEnergyApi.Dtos;

public record SignupRequest(string? Name, string? Email, string? Password);
public record LoginRequest(string? Email, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record ForgotPasswordRequest(string? Email);
public record ResetPasswordRequest(string? Email, string? Code, string? NewPassword);

public record PublicUserDto(int Id, string Name, string Email);
public record AuthResponse(string Token, PublicUserDto User);

/// <summary>Delete (schedule deletion of) the signed-in account: confirmed with the e-mail and the password, or with a one-time code e-mailed to the account (for Google accounts).</summary>
public record DeleteAccountRequest(string? Email, string? Password, string? Code = null);
public record DeleteCodeRequest(string? Lang);
/// <summary>Restore a deleted account within 30 days: with the password, or (Google accounts) with a Google id token.</summary>
public record RestoreAccountRequest(string? Email, string? Password, string? IdToken);
