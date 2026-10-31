using System.Text.Json;

namespace SmartEnergyApi.Services;

public record GoogleIdentity(string Sub, string Email);

/// <summary>
/// Verifies a Google ID token (obtained by the mobile app through Google Sign-In) with Google's tokeninfo
/// endpoint and checks it was issued for one of OUR OAuth client ids (Google:ClientIds, comma separated).
/// </summary>
public class GoogleAuthService
{
    private readonly IHttpClientFactory _http;
    private readonly HashSet<string> _clientIds;

    public GoogleAuthService(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _clientIds = (config["Google:ClientIds"] ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
    }

    public bool IsConfigured => _clientIds.Count > 0;

    public async Task<GoogleIdentity?> VerifyAsync(string? idToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken)) return null;
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            var res = await client.GetAsync("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken));
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string Str(string name) => root.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";

            if (!_clientIds.Contains(Str("aud"))) return null;
            var iss = Str("iss");
            if (iss != "accounts.google.com" && iss != "https://accounts.google.com") return null;
            if (!string.Equals(Str("email_verified"), "true", StringComparison.OrdinalIgnoreCase)) return null;
            var sub = Str("sub");
            var email = Str("email");
            if (sub == "" || email == "") return null;
            return new GoogleIdentity(sub, email.ToLowerInvariant());
        }
        catch
        {
            return null;
        }
    }
}
