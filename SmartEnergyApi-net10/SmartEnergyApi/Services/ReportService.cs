using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Services;

/// <summary>Builds the weekly / monthly consumption report of a user and e-mails it (English or Arabic).</summary>
public class ReportService
{
    private readonly AppDbContext _db;
    private readonly TariffService _tariff;
    private readonly CardService _card;
    private readonly EmailService _email;
    private readonly int _utcOffset;

    public ReportService(AppDbContext db, TariffService tariff, CardService card, EmailService email, IConfiguration config)
    {
        _db = db;
        _tariff = tariff;
        _card = card;
        _email = email;
        // Egypt is UTC+2 (UTC+3 in summer): used for day boundaries and the dates shown in the e-mail.
        _utcOffset = int.TryParse(config["Reports:UtcOffsetHours"], out var h) ? h : 2;
    }

    public bool MailConfigured => _email.IsConfigured;

    private record Sample(int DeviceId, DateTime Ts, double Power);
    private record DeviceRow(string Name, string Room, double Kwh);

    private class ReportData
    {
        public string Period = "weekly";
        public DateTime Start, End;
        public double Kwh, PrevKwh, Cost, Price;
        public List<DeviceRow> Devices = new();
        public List<(string Label, double Kwh)> Buckets = new();
        public bool HasCard;
        public double CardBalance;
        public int Tier;
        public double TierPrice;
    }

    private DateTime Local(DateTime utc) => utc.AddHours(_utcOffset);

    /// <param name="mtd">Monthly report only: true = this month so far (manual "send now"), false = the previous full month.</param>
    public async Task SendReportAsync(User user, string period, bool mtd, string lang)
    {
        var data = await CollectAsync(user, period, mtd);
        var (subject, html) = Render(data, lang);
        await _email.SendAsync(user.Email, subject, html);
    }

    private async Task<ReportData> CollectAsync(User user, string period, bool mtd)
    {
        var now = DateTime.UtcNow;
        DateTime start, end;
        double bucketDays;
        if (period == "weekly")
        {
            end = now;
            start = now.AddDays(-7);
            bucketDays = 1;
        }
        else
        {
            var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (mtd) { start = thisMonth; end = now; }
            else { start = thisMonth.AddMonths(-1); end = thisMonth; }
            bucketDays = 7;
        }
        var span = end - start;
        var prevStart = start - span;
        var bucketCount = Math.Max(1, (int)Math.Ceiling(span.TotalDays / bucketDays));

        var devices = await _db.Devices.Where(d => d.UserId == user.Id).ToListAsync();
        var ids = devices.Select(d => d.Id).ToList();
        var samples = await _db.Readings
            .Where(r => ids.Contains(r.DeviceId) && r.Timestamp >= prevStart && r.Timestamp < end)
            .Select(r => new Sample(r.DeviceId, r.Timestamp, r.Power))
            .ToListAsync();

        double cur = 0, prev = 0;
        var perDevice = new Dictionary<int, double>();
        var buckets = new double[bucketCount];
        foreach (var g in samples.GroupBy(s => s.DeviceId))
        {
            var list = g.OrderBy(s => s.Ts).ToList();
            for (int i = 1; i < list.Count; i++)
            {
                var hours = (list[i].Ts - list[i - 1].Ts).TotalHours;
                if (hours <= 0 || hours > 6) continue;
                var kwh = (list[i - 1].Power + list[i].Power) / 2.0 / 1000.0 * hours;
                var ts = list[i].Ts;
                if (ts >= start)
                {
                    cur += kwh;
                    perDevice[g.Key] = perDevice.GetValueOrDefault(g.Key) + kwh;
                    var bi = (int)((ts - start).TotalDays / bucketDays);
                    if (bi >= 0 && bi < bucketCount) buckets[bi] += kwh;
                }
                else
                {
                    prev += kwh;
                }
            }
        }

        var data = new ReportData { Period = period, Start = start, End = end, Kwh = cur, PrevKwh = prev };

        // Cost: a full month is priced tier by tier; a week uses the price of the tier the user is in now.
        var tierNow = await _tariff.GetTierPosAsync(user.MonthKwh);
        data.Tier = tierNow.Index + 1;
        data.TierPrice = tierNow.Price;
        data.Cost = period == "monthly" ? await _tariff.EstimateCostAsync(cur) : cur * tierNow.Price;
        data.Price = cur > 0 ? data.Cost / cur : tierNow.Price;

        data.Devices = devices
            .Select(d => new DeviceRow(d.Name, d.Room, perDevice.GetValueOrDefault(d.Id)))
            .Where(r => r.Kwh > 0)
            .OrderByDescending(r => r.Kwh)
            .ToList();

        for (int i = 0; i < bucketCount; i++)
        {
            var bStart = start.AddDays(i * bucketDays);
            var bEnd = i == bucketCount - 1 ? end : start.AddDays((i + 1) * bucketDays);
            var label = period == "weekly"
                ? Local(bStart).ToString("MM-dd", CultureInfo.InvariantCulture)
                : Local(bStart).ToString("MM-dd", CultureInfo.InvariantCulture) + " → " + Local(bEnd.AddSeconds(-1)).ToString("MM-dd", CultureInfo.InvariantCulture);
            data.Buckets.Add((label, buckets[i]));
        }

        var card = await _card.GetSummaryAsync(user.Id);
        data.HasCard = card.HasCard;
        data.CardBalance = card.Balance;
        return data;
    }

    private static string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
    private static string E(string s) => WebUtility.HtmlEncode(s);

    private (string Subject, string Html) Render(ReportData d, string lang)
    {
        var ar = lang == "ar";
        string T(string en, string arText) => ar ? arText : en;

        var title = d.Period == "weekly"
            ? T("Your weekly energy report", "تقرير الاستهلاك الأسبوعي")
            : T("Your monthly energy report", "تقرير الاستهلاك الشهري");
        var range = Local(d.Start).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " → " + Local(d.End).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var subject = "Smart Energy — " + title + " (" + range + ")";

        string change = "";
        if (d.PrevKwh > 0)
        {
            var pct = (d.Kwh - d.PrevKwh) / d.PrevKwh * 100;
            var color = pct > 0 ? "#dc2626" : "#16a34a";
            var arrow = pct > 0 ? "▲" : "▼";
            change = $"<div style=\"font-size:13px;color:{color};margin-top:4px\">{arrow} {N(Math.Abs(pct), "0")}% {E(T("vs previous period", "مقارنة بالفترة السابقة"))}</div>";
        }

        var sb = new StringBuilder();
        sb.Append($"<div dir=\"{(ar ? "rtl" : "ltr")}\" style=\"font-family:Arial,Helvetica,sans-serif;background:#f3f4f6;padding:20px\">");
        sb.Append("<div style=\"max-width:560px;margin:auto;background:#ffffff;border-radius:14px;padding:22px;color:#111827\">");
        sb.Append($"<h2 style=\"margin:0 0 4px 0;color:#0b2a4a\">{E(title)}</h2>");
        sb.Append($"<div style=\"font-size:13px;color:#6b7280;margin-bottom:16px\">{E(T("Period", "الفترة"))}: {E(range)}</div>");

        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin-bottom:18px\"><tr>");
        sb.Append($"<td style=\"background:#eff6ff;border-radius:10px;padding:14px;width:50%\"><div style=\"font-size:12px;color:#6b7280\">{E(T("Total consumption", "إجمالي الاستهلاك"))}</div>");
        sb.Append($"<div style=\"font-size:24px;font-weight:bold;color:#1d4ed8\">{N(d.Kwh)} {E(T("kWh", "ك.و.س"))}</div>{change}</td>");
        sb.Append("<td style=\"width:10px\"></td>");
        sb.Append($"<td style=\"background:#ecfdf5;border-radius:10px;padding:14px;width:50%\"><div style=\"font-size:12px;color:#6b7280\">{E(T("Estimated cost", "التكلفة التقديرية"))}</div>");
        sb.Append($"<div style=\"font-size:24px;font-weight:bold;color:#047857\">{N(d.Cost)} {E(T("EGP", "جنيه"))}</div></td>");
        sb.Append("</tr></table>");

        if (d.Kwh <= 0)
        {
            sb.Append($"<p style=\"color:#6b7280\">{E(T("No consumption was recorded in this period.", "لا يوجد استهلاك مسجّل في هذه الفترة."))}</p>");
        }
        else
        {
            sb.Append($"<h3 style=\"margin:18px 0 8px 0;font-size:15px\">{E(T("By device", "حسب الجهاز"))}</h3>");
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            sb.Append($"<tr style=\"color:#6b7280\"><td style=\"padding:6px 0\">{E(T("Device", "الجهاز"))}</td><td>{E(T("kWh", "ك.و.س"))}</td><td>{E(T("Cost (EGP)", "التكلفة (جنيه)"))}</td><td>%</td></tr>");
            foreach (var r in d.Devices)
            {
                var share = r.Kwh / d.Kwh;
                var name = r.Room.Length > 0 ? r.Name + " · " + r.Room : r.Name;
                sb.Append($"<tr style=\"border-top:1px solid #e5e7eb\"><td style=\"padding:7px 0\">{E(name)}</td><td>{N(r.Kwh)}</td><td>{N(d.Cost * share)}</td><td>{N(share * 100, "0")}%</td></tr>");
            }
            sb.Append("</table>");

            var max = Math.Max(d.Buckets.Max(b => b.Kwh), 0.0001);
            sb.Append($"<h3 style=\"margin:18px 0 8px 0;font-size:15px\">{E(d.Period == "weekly" ? T("By day", "حسب اليوم") : T("By week", "حسب الأسبوع"))}</h3>");
            sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\">");
            foreach (var b in d.Buckets)
            {
                var w = (int)Math.Round(b.Kwh / max * 100);
                sb.Append($"<tr><td style=\"padding:4px 0;width:34%;color:#6b7280\">{E(b.Label)}</td>");
                sb.Append($"<td><div style=\"background:#3b82f6;height:8px;border-radius:4px;width:{w}%\"></div></td>");
                sb.Append($"<td style=\"width:22%;text-align:{(ar ? "left" : "right")}\">{N(b.Kwh)} {E(T("kWh", "ك.و.س"))}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append($"<h3 style=\"margin:18px 0 8px 0;font-size:15px\">{E(T("Your tariff", "تعريفتك"))}</h3>");
        sb.Append($"<div style=\"font-size:13px\">{E(T("Tariff tier", "شريحة التعريفة"))} {d.Tier} · {N(d.TierPrice)} {E(T("EGP/kWh", "جنيه/ك.و.س"))}</div>");
        if (d.HasCard)
            sb.Append($"<div style=\"font-size:13px;margin-top:4px\">{E(T("Card balance left", "المتبقي في الكارت"))}: <b>{N(d.CardBalance)} {E(T("EGP", "جنيه"))}</b></div>");

        sb.Append($"<p style=\"font-size:11px;color:#9ca3af;margin-top:20px\">{E(T("Estimates based on your plug readings, not an official bill.", "تقديرات مبنية على قراءات الفيشات وليست فاتورة رسمية."))}</p>");
        sb.Append("</div></div>");
        return (subject, sb.ToString());
    }
}
