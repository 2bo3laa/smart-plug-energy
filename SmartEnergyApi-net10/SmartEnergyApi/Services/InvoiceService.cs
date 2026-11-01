using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>
/// Estimated invoice for the whole home or for one device, for the last 7 days ("weekly") or this month so far
/// ("monthly"). Monthly invoices are priced tier by tier; weekly ones use the price of the tier the user is in now.
/// It is an estimate from the plug readings, not an official bill.
/// </summary>
public class InvoiceService
{
    private readonly AppDbContext _db;
    private readonly TariffService _tariff;
    private readonly EmailService _email;
    private readonly int _utcOffset;

    public InvoiceService(AppDbContext db, TariffService tariff, EmailService email, IConfiguration config)
    {
        _db = db;
        _tariff = tariff;
        _email = email;
        _utcOffset = int.TryParse(config["Reports:UtcOffsetHours"], out var h) ? h : 2;
    }

    public bool MailConfigured => _email.IsConfigured;

    private record Sample(int DeviceId, DateTime Ts, double Power);

    private DateTime Local(DateTime utc) => utc.AddHours(_utcOffset);

    /// <returns>null when deviceId does not belong to the user.</returns>
    public async Task<InvoiceDto?> BuildAsync(User user, string period, int? deviceId)
    {
        var now = DateTime.UtcNow;
        var monthly = period == "monthly";
        var from = monthly ? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc) : now.AddDays(-7);
        var to = now;

        var devices = await _db.Devices.Where(d => d.UserId == user.Id).ToListAsync();
        if (deviceId != null && devices.All(d => d.Id != deviceId)) return null;
        var ids = devices.Select(d => d.Id).ToList();

        var samples = await _db.Readings
            .Where(r => ids.Contains(r.DeviceId) && r.Timestamp >= from && r.Timestamp < to)
            .Select(r => new Sample(r.DeviceId, r.Timestamp, r.Power))
            .ToListAsync();

        var kwh = new Dictionary<int, double>();
        var hoursOn = new Dictionary<int, double>();
        var peak = new Dictionary<int, double>();
        foreach (var g in samples.GroupBy(s => s.DeviceId))
        {
            var list = g.OrderBy(s => s.Ts).ToList();
            peak[g.Key] = list.Max(s => s.Power);
            for (int i = 1; i < list.Count; i++)
            {
                var h = (list[i].Ts - list[i - 1].Ts).TotalHours;
                if (h <= 0 || h > 6) continue;
                kwh[g.Key] = kwh.GetValueOrDefault(g.Key) + (list[i - 1].Power + list[i].Power) / 2.0 / 1000.0 * h;
                if (list[i].Power > 5 || list[i - 1].Power > 5) hoursOn[g.Key] = hoursOn.GetValueOrDefault(g.Key) + h;
            }
        }
        var homeKwh = kwh.Values.Sum();

        // ---- price the whole home, then split the money between devices by their share of the energy ----
        var bands = new List<InvoiceBandDto>();
        double homeCost;
        int tierNumber;
        if (monthly)
        {
            var tiers = await _db.TariffTiers.OrderBy(t => t.MinKwh).ToListAsync();
            if (tiers.Count == 0)
            {
                var flat = (await _tariff.GetTierInfoAsync(0)).CurrentPrice;
                homeCost = homeKwh * flat;
                tierNumber = 1;
                bands.Add(new InvoiceBandDto(1, 0, null, flat, homeKwh, homeCost));
            }
            else
            {
                double remaining = homeKwh;
                homeCost = 0;
                int n = 0;
                foreach (var t in tiers)
                {
                    n++;
                    if (remaining <= 0) break;
                    var width = (t.MaxKwh ?? double.MaxValue) - t.MinKwh;
                    var used = Math.Min(remaining, width);
                    if (used <= 0) continue;
                    bands.Add(new InvoiceBandDto(n, t.MinKwh, t.MaxKwh, t.PricePerKwh, used, used * t.PricePerKwh));
                    homeCost += used * t.PricePerKwh;
                    remaining -= used;
                }
                if (remaining > 0 && bands.Count > 0)
                {
                    // Beyond the last defined tier the top rate keeps applying.
                    var last = bands[^1];
                    bands[^1] = last with { Kwh = last.Kwh + remaining, CostEgp = last.CostEgp + remaining * last.Price };
                    homeCost += remaining * last.Price;
                }
                tierNumber = bands.Count > 0 ? bands[^1].Tier : 1;
            }
        }
        else
        {
            var info = await _tariff.GetTierInfoAsync(user.MonthKwh);
            var pos = await _tariff.GetTierPosAsync(user.MonthKwh);
            homeCost = homeKwh * info.CurrentPrice;
            tierNumber = pos.Index + 1;
        }
        bands = bands.Select(b => b with { Kwh = Math.Round(b.Kwh, 3), CostEgp = Math.Round(b.CostEgp, 2) }).ToList();

        var lines = devices
            .Where(d => deviceId == null ? kwh.GetValueOrDefault(d.Id) > 0 : d.Id == deviceId)
            .Select(d =>
            {
                var k = kwh.GetValueOrDefault(d.Id);
                var cost = homeKwh > 0 ? homeCost * k / homeKwh : 0;
                return new InvoiceLineDto(
                    d.Id, d.Name, d.Room,
                    Math.Round(hoursOn.GetValueOrDefault(d.Id), 1),
                    Math.Round(peak.GetValueOrDefault(d.Id), 0),
                    Math.Round(k, 3), Math.Round(cost, 2),
                    homeKwh > 0 ? Math.Round(k / homeKwh * 100, 1) : 0);
            })
            .OrderByDescending(l => l.Kwh)
            .ToList();

        var number = "INV-" + Local(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + user.Id;
        return new InvoiceDto(
            number, monthly ? "monthly" : "weekly", from, to, deviceId != null,
            Math.Round(lines.Sum(l => l.Kwh), 3), Math.Round(lines.Sum(l => l.CostEgp), 2),
            Math.Round(homeKwh, 3), Math.Round(homeCost, 2),
            homeKwh > 0 ? Math.Round(homeCost / homeKwh, 4) : (await _tariff.GetTierInfoAsync(user.MonthKwh)).CurrentPrice,
            tierNumber, lines, bands);
    }

    public async Task SendEmailAsync(User user, InvoiceDto inv, string lang)
    {
        var (subject, html) = Render(user, inv, lang);
        await _email.SendAsync(user.Email, subject, html);
    }

    private static string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
    private static string E(string s) => WebUtility.HtmlEncode(s);

    private (string Subject, string Html) Render(User user, InvoiceDto inv, string lang)
    {
        var ar = lang == "ar";
        string T(string en, string arText) => ar ? arText : en;

        var title = inv.SingleDevice ? T("Estimated device invoice", "فاتورة تقديرية للجهاز") : T("Estimated invoice", "فاتورة تقديرية");
        var periodName = inv.Period == "weekly" ? T("Weekly", "أسبوعية") : T("Monthly", "شهرية");
        var range = Local(inv.From).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " → " + Local(inv.To).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var subject = "Smart Energy — " + title + " (" + periodName + ", " + range + ")";

        var sb = new StringBuilder();
        sb.Append($"<div dir=\"{(ar ? "rtl" : "ltr")}\" style=\"font-family:Arial,Helvetica,sans-serif;background:#f3f4f6;padding:20px\">");
        sb.Append("<div style=\"max-width:560px;margin:auto;background:#ffffff;border-radius:14px;padding:22px;color:#111827\">");
        sb.Append($"<h2 style=\"margin:0 0 4px 0;color:#0b2a4a\">{E(title)}</h2>");
        sb.Append($"<div style=\"font-size:13px;color:#6b7280\">{E(inv.Number)} · {E(periodName)}</div>");
        sb.Append($"<div style=\"font-size:13px;color:#6b7280;margin-bottom:4px\">{E(T("Period", "الفترة"))}: {E(range)}</div>");
        sb.Append($"<div style=\"font-size:13px;color:#6b7280;margin-bottom:16px\">{E(T("Customer", "العميل"))}: {E(user.Name)} ({E(user.Email)})</div>");

        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
        sb.Append($"<tr style=\"color:#6b7280\"><td style=\"padding:6px 0\">{E(T("Device", "الجهاز"))}</td><td>{E(T("Hours", "ساعات"))}</td><td>{E(T("kWh", "ك.و.س"))}</td><td>{E(T("EGP", "جنيه"))}</td></tr>");
        foreach (var l in inv.Lines)
        {
            var name = l.Room.Length > 0 ? l.Name + " · " + l.Room : l.Name;
            sb.Append($"<tr style=\"border-top:1px solid #e5e7eb\"><td style=\"padding:7px 0\">{E(name)}</td><td>{N(l.Hours, "0.0")}</td><td>{N(l.Kwh)}</td><td>{N(l.CostEgp)}</td></tr>");
        }
        sb.Append($"<tr style=\"border-top:2px solid #111827;font-weight:bold\"><td style=\"padding:8px 0\">{E(T("Total", "الإجمالي"))}</td><td></td><td>{N(inv.TotalKwh)}</td><td>{N(inv.TotalCostEgp)}</td></tr>");
        sb.Append("</table>");

        if (inv.Bands.Count > 0)
        {
            sb.Append($"<h3 style=\"margin:18px 0 8px 0;font-size:15px\">{E(T("Tariff breakdown (whole home)", "تفصيل الشرائح (المنزل كله)"))}</h3>");
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            foreach (var b in inv.Bands)
            {
                var rangeText = b.ToKwh == null ? N(b.FromKwh, "0") + "+" : N(b.FromKwh, "0") + " – " + N(b.ToKwh.Value, "0");
                sb.Append($"<tr style=\"border-top:1px solid #e5e7eb\"><td style=\"padding:6px 0\">{E(T("Tier", "شريحة"))} {b.Tier} ({E(rangeText)} {E(T("kWh", "ك.و.س"))})</td><td>{N(b.Kwh)} × {N(b.Price)}</td><td>{N(b.CostEgp)} {E(T("EGP", "جنيه"))}</td></tr>");
            }
            sb.Append("</table>");
        }
        else
        {
            sb.Append($"<div style=\"font-size:13px;margin-top:14px\">{E(T("Weekly estimate uses your current tier price", "التقدير الأسبوعي بسعر شريحتك الحالية"))}: {N(inv.PricePerKwh)} {E(T("EGP/kWh", "جنيه/ك.و.س"))}</div>");
        }
        if (inv.SingleDevice)
            sb.Append($"<div style=\"font-size:13px;margin-top:10px;color:#6b7280\">{E(T("Whole home in this period", "إجمالي المنزل في هذه الفترة"))}: {N(inv.HomeKwh)} {E(T("kWh", "ك.و.س"))} · {N(inv.HomeCostEgp)} {E(T("EGP", "جنيه"))}</div>");
        sb.Append($"<p style=\"font-size:11px;color:#9ca3af;margin-top:20px\">{E(T("Estimated invoice from your plug readings, not an official bill.", "فاتورة تقديرية من قراءات الفيشات وليست فاتورة رسمية."))}</p>");
        sb.Append("</div></div>");
        return (subject, sb.ToString());
    }
}
