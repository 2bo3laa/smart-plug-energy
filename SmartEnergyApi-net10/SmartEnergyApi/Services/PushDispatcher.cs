using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;

namespace SmartEnergyApi.Services;

public record PushItem(int AlertId, int UserId, int? DeviceId, string Type, string Title, string Message);

/// <summary>Arabic text for the server-made alert messages (the phone shows them without the app translating).</summary>
public static class AlertTexts
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["High Power Usage"] = "استهلاك قدرة مرتفع",
        ["Above rated power"] = "أعلى من القدرة المقررة",
        ["Moved to a higher tariff tier"] = "انتقلت إلى شريحة أعلى في التعريفة",
        ["Approaching a higher tariff tier"] = "اقتربت من شريحة أعلى في التعريفة",
        ["Card balance is low"] = "رصيد الكارت على وشك الانتهاء",
        ["Card balance is empty"] = "رصيد الكارت انتهى",
        ["Voltage Fluctuation"] = "تذبذب في الجهد",
        ["High Energy Usage"] = "استهلاك طاقة مرتفع",
    };

    private static readonly (Regex Re, string Replacement)[] Rules =
    {
        (new Regex(@"^(.+) exceeded (.+)W \(reading: (.+)W\)$"), "$1 تجاوز $2 واط (القراءة: $3 واط)"),
        (new Regex(@"^(.+) is drawing (.+)W, (\d+)% above its rated (.+)W\. Check the appliance\.$"), "$1 يسحب $2 واط، أعلى بنسبة $3٪ من قدرته المقررة $4 واط. افحص الجهاز."),
        (new Regex(@"^You have used (.+) kWh this month and moved into tier (\d+) \((.+) EGP/kWh\)\.$"), "استهلكت $1 ك.و.س هذا الشهر وانتقلت إلى الشريحة $2 ($3 جنيه/ك.و.س)."),
        (new Regex(@"^At this pace you will use about (.+) kWh this month and enter a tier at (.+) EGP/kWh \((.+) kWh left in your current tier\)\.$"), "بهذا المعدل ستستهلك حوالي $1 ك.و.س هذا الشهر وستدخل شريحة بسعر $2 جنيه/ك.و.س (متبقي $3 ك.و.س في شريحتك الحالية)."),
        (new Regex(@"^Only (.+) EGP is left on your electricity card\.$"), "متبقي $1 جنيه فقط على كارت الكهرباء."),
        (new Regex(@"^Your electricity card balance has run out\. Recharge it to avoid a power cut\.$"), "انتهى رصيد كارت الكهرباء. اشحنه لتجنب انقطاع الكهرباء."),
    };

    public static (string Title, string Body) Localize(string title, string message, string? lang)
    {
        if (lang != "ar") return (title, message);
        var t = Titles.TryGetValue(title, out var ar) ? ar : title;
        var b = message;
        foreach (var (re, replacement) in Rules)
        {
            if (re.IsMatch(message))
            {
                b = re.Replace(message, replacement);
                break;
            }
        }
        return (t, b);
    }
}

/// <summary>
/// Called by AppDbContext after new alerts are saved: sends each one as a phone notification to the user's registered phone.
/// Best effort and in the background, so it never slows down or breaks the request that created the alert.
/// </summary>
public static class PushDispatcher
{
    public static IServiceScopeFactory? Scopes { get; set; }
    public static PushService? Push { get; set; }

    public static void Dispatch(IReadOnlyList<PushItem> items)
    {
        var scopes = Scopes;
        var push = Push;
        if (scopes == null || push == null || !push.IsConfigured) return;

        // Switching a plug on/off yourself in the app does not need a phone notification.
        var list = items.Where(i => i.Type != "device_on" && i.Type != "device_off").ToList();
        if (list.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                foreach (var group in list.GroupBy(i => i.UserId))
                {
                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == group.Key);
                    if (user == null || string.IsNullOrEmpty(user.PushToken)) continue;
                    foreach (var item in group)
                    {
                        var (title, body) = AlertTexts.Localize(item.Title, item.Message, user.PushLang);
                        var data = new Dictionary<string, string>
                        {
                            ["alertId"] = item.AlertId.ToString(),
                            ["type"] = item.Type,
                            ["deviceId"] = item.DeviceId?.ToString() ?? ""
                        };
                        var result = await push.SendAsync(user.PushToken, title, body, data);
                        if (result == PushResult.InvalidToken)
                        {
                            user.PushToken = null;
                            await db.SaveChangesAsync();
                            break;
                        }
                    }
                }
            }
            catch
            {
                // push is best effort
            }
        });
    }
}
