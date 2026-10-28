using System.Security.Claims;

namespace SmartEnergyApi.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("userId") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (value == null || !int.TryParse(value, out var id))
        {
            throw new UnauthorizedAccessException("Token does not contain a valid userId claim.");
        }
        return id;
    }
}
