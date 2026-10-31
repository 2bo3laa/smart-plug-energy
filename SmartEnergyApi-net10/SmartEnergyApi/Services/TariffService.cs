using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;

namespace SmartEnergyApi.Services;

public record TierInfo(double CurrentPrice, double? NextPrice, double? KwhUntilNext);
public record TierPos(int Index, double MinKwh, double Price);

public class TariffService
{
    private readonly AppDbContext _db;
    private readonly double _fallbackPricePerKwh;

    public TariffService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _fallbackPricePerKwh = double.TryParse(config["Tariff:DefaultPricePerKwh"], out var p) ? p : 1.65;
    }

    /// <summary>
    /// Applies Egypt's stepped residential tariff structure: consumption is
    /// costed tier-by-tier (the first N kWh at the cheapest rate, the next
    /// band at the next rate, and so on) rather than a single flat rate.
    /// </summary>
    /// <summary>Which tier the given monthly consumption is in, the next tier's price, and kWh left before it.</summary>
    public async Task<TierInfo> GetTierInfoAsync(double kwh)
    {
        var tiers = await _db.TariffTiers.OrderBy(t => t.MinKwh).ToListAsync();
        if (tiers.Count == 0) return new TierInfo(_fallbackPricePerKwh, null, null);
        var cur = tiers.LastOrDefault(t => kwh >= t.MinKwh) ?? tiers[0];
        var next = cur.MaxKwh.HasValue ? tiers.FirstOrDefault(t => t.MinKwh >= cur.MaxKwh.Value) : null;
        return new TierInfo(
            cur.PricePerKwh,
            next?.PricePerKwh,
            cur.MaxKwh.HasValue ? (double?)Math.Max(0, cur.MaxKwh.Value - kwh) : null);
    }

    /// <summary>Position (0-based index, lower bound, price) of the tier a monthly consumption falls in.</summary>
    public async Task<TierPos> GetTierPosAsync(double kwh)
    {
        var tiers = await _db.TariffTiers.OrderBy(t => t.MinKwh).ToListAsync();
        if (tiers.Count == 0) return new TierPos(0, 0, _fallbackPricePerKwh);
        int idx = 0;
        for (int i = 0; i < tiers.Count; i++) if (kwh >= tiers[i].MinKwh) idx = i;
        return new TierPos(idx, tiers[idx].MinKwh, tiers[idx].PricePerKwh);
    }

    public async Task<double> EstimateCostAsync(double totalKwhThisMonth)
    {
        var tiers = await _db.TariffTiers.OrderBy(t => t.MinKwh).ToListAsync();
        if (tiers.Count == 0)
        {
            return Math.Round(totalKwhThisMonth * _fallbackPricePerKwh, 2);
        }

        double remaining = totalKwhThisMonth;
        double cost = 0;

        foreach (var tier in tiers)
        {
            if (remaining <= 0) break;
            var tierWidth = (tier.MaxKwh ?? double.MaxValue) - tier.MinKwh;
            var kwhInTier = Math.Min(remaining, tierWidth);
            if (kwhInTier <= 0) continue;
            cost += kwhInTier * tier.PricePerKwh;
            remaining -= kwhInTier;
        }

        // Anything beyond the last defined tier falls back to the top tier's rate.
        if (remaining > 0 && tiers.Count > 0)
        {
            cost += remaining * tiers[^1].PricePerKwh;
        }

        return Math.Round(cost, 2);
    }
}
