namespace SmartEnergyApi.Models;

public class PasswordReset
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
