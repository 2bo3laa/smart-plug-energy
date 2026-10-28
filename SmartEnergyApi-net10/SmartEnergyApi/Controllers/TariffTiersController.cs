using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Models;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/tariff-tiers")]
[Authorize]
public class TariffTiersController : ControllerBase
{
    private readonly AppDbContext _db;

    public TariffTiersController(AppDbContext db)
    {
        _db = db;
    }

    private static TariffTierDto ToDto(TariffTier t) => new(t.Id, t.MinKwh, t.MaxKwh, t.PricePerKwh);

    private async Task<bool> IsAdminAsync()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        return user?.Role == "admin";
    }

    // GET /api/tariff-tiers  (any logged-in user can view the current tariff)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tiers = await _db.TariffTiers.OrderBy(t => t.MinKwh).ToListAsync();
        return Ok(new { tiers = tiers.Select(ToDto).ToList() });
    }

    // POST /api/tariff-tiers  (admin only)
    [HttpPost]
    public async Task<IActionResult> Create(UpsertTariffTierRequest req)
    {
        if (!await IsAdminAsync()) return Forbid();

        var tier = new TariffTier { MinKwh = req.MinKwh, MaxKwh = req.MaxKwh, PricePerKwh = req.PricePerKwh };
        _db.TariffTiers.Add(tier);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { tier = ToDto(tier) });
    }

    // PATCH /api/tariff-tiers/{id}  (admin only)
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertTariffTierRequest req)
    {
        if (!await IsAdminAsync()) return Forbid();

        var tier = await _db.TariffTiers.FindAsync(id);
        if (tier == null) return NotFound(new { error = "Tariff tier not found" });

        tier.MinKwh = req.MinKwh;
        tier.MaxKwh = req.MaxKwh;
        tier.PricePerKwh = req.PricePerKwh;
        await _db.SaveChangesAsync();

        return Ok(new { tier = ToDto(tier) });
    }

    // DELETE /api/tariff-tiers/{id}  (admin only)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await IsAdminAsync()) return Forbid();

        var tier = await _db.TariffTiers.FindAsync(id);
        if (tier == null) return NotFound(new { error = "Tariff tier not found" });

        _db.TariffTiers.Remove(tier);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
