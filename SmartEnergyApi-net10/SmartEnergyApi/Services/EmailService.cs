using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace SmartEnergyApi.Services;

/// <summary>
/// Sends e-mail. Configured in secrets.local.json (never on GitHub):
///   Gmail:  "Mail": { "User": "you@gmail.com", "Password": "<16-char app password>" }
///   Brevo:  "Mail": { "BrevoApiKey": "...", "From": "verified-sender@example.com" }   (HTTPS API, works where SMTP is blocked)
/// </summary>
public class EmailService
{
    private readonly IHttpClientFactory _http;
    private readonly string _host, _user, _pass, _from, _fromName, _brevoKey;
    private readonly int _port;

    public EmailService(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _host = config["Mail:Host"] ?? "smtp.gmail.com";
        _port = int.TryParse(config["Mail:Port"], out var p) ? p : 587;
        _user = config["Mail:User"] ?? "";
        _pass = (config["Mail:Password"] ?? "").Replace(" ", "");   // Gmail shows app passwords with spaces
        _from = string.IsNullOrWhiteSpace(config["Mail:From"]) ? _user : config["Mail:From"]!.Trim();
        _fromName = config["Mail:FromName"] ?? "Smart Energy";
        _brevoKey = config["Mail:BrevoApiKey"] ?? "";
    }

    public bool IsConfigured => _brevoKey != "" ? _from != "" : (_user != "" && _pass != "");

    public async Task SendAsync(string to, string subject, string html)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email sending is not configured on the server");

        if (_brevoKey != "")
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            req.Headers.Add("api-key", _brevoKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    sender = new { name = _fromName, email = _from },
                    to = new[] { new { email = to } },
                    subject,
                    htmlContent = html
                }),
                Encoding.UTF8, "application/json");
            var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException("Email service error " + (int)res.StatusCode + ": " + await res.Content.ReadAsStringAsync());
            return;
        }

        using var smtp = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _pass),
            Timeout = 20000
        };
        using var msg = new MailMessage
        {
            From = new MailAddress(_from, _fromName, Encoding.UTF8),
            Subject = subject,
            Body = html,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        msg.To.Add(to);
        await smtp.SendMailAsync(msg);
    }
}
