using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartEnergyApi.Services;

public enum PushResult { Sent, InvalidToken, Failed }

/// <summary>
/// Sends phone notifications through Firebase Cloud Messaging (HTTP v1 API).
/// It needs the Firebase service-account key file (firebase-service-account.json) next to the app on the server.
/// No extra NuGet package: the Google access token is made by signing a JWT with the key from that file.
/// </summary>
public class PushService
{
    private readonly IHttpClientFactory _http;
    private readonly string _fileName;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _projectId, _clientEmail, _privateKey;
    private string? _accessToken;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;

    public string? LastError { get; private set; }

    public PushService(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _fileName = string.IsNullOrWhiteSpace(config["Push:ServiceAccountFile"]) ? "firebase-service-account.json" : config["Push:ServiceAccountFile"]!;
    }

    private string? FindFile()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var path = Path.Combine(dir, _fileName);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public bool IsConfigured => FindFile() != null;

    private bool LoadCredentials()
    {
        if (_privateKey != null) return true;
        var path = FindFile();
        if (path == null) return false;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        _projectId = root.GetProperty("project_id").GetString();
        _clientEmail = root.GetProperty("client_email").GetString();
        _privateKey = root.GetProperty("private_key").GetString();
        return _privateKey != null && _projectId != null && _clientEmail != null;
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<string> GetAccessTokenAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiresUtc.AddMinutes(-2)) return _accessToken;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
            var claims = B64(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = _clientEmail,
                scope = "https://www.googleapis.com/auth/firebase.messaging",
                aud = "https://oauth2.googleapis.com/token",
                iat = now,
                exp = now + 3600
            }));
            var unsigned = header + "." + claims;
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_privateKey!);
            var signature = B64(rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var jwt = unsigned + "." + signature;

            var client = _http.CreateClient();
            var res = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt
            }));
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException("Google token error " + (int)res.StatusCode + ": " + text);

            using var doc = JsonDocument.Parse(text);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expires = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(expires);
            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PushResult> SendAsync(string deviceToken, string title, string body, Dictionary<string, string> data)
    {
        try
        {
            if (!LoadCredentials())
            {
                LastError = "firebase-service-account.json was not found next to the app";
                return PushResult.Failed;
            }
            var access = await GetAccessTokenAsync();
            var payload = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title, body },
                    data,
                    android = new { priority = "HIGH", notification = new { sound = "default" } }
                }
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/v1/projects/" + _projectId + "/messages:send");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.CreateClient().SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                LastError = null;
                return PushResult.Sent;
            }
            LastError = (int)res.StatusCode + ": " + text;
            if (text.Contains("UNREGISTERED") || text.Contains("NOT_FOUND")) return PushResult.InvalidToken;
            return PushResult.Failed;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return PushResult.Failed;
        }
    }
}
