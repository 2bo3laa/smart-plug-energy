using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Authorize]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _db;
    public CatalogController(AppDbContext db) { _db = db; }

    // GET /api/catalog/types
    [HttpGet("types")]
    public async Task<IActionResult> Types()
    {
        var counts = await _db.CatalogItems.GroupBy(c => c.TypeKey)
            .Select(g => new { Key = g.Key, N = g.Count() }).ToListAsync();
        var types = await _db.ApplianceTypes.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync();
        var list = types.Select(t => new CatalogTypeDto(
            t.Key, t.Name, t.DefaultWatts, counts.FirstOrDefault(c => c.Key == t.Key)?.N ?? 0)).ToList();
        return Ok(new { types = list });
    }

    // GET /api/catalog/brands?type=refrigerator
    [HttpGet("brands")]
    public async Task<IActionResult> Brands([FromQuery] string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return BadRequest(new { error = "type is required" });
        var brands = await _db.CatalogItems.AsNoTracking()
            .Where(c => c.TypeKey == type)
            .GroupBy(c => c.Brand)
            .Select(g => new CatalogBrandDto(g.Key, g.Count()))
            .ToListAsync();
        return Ok(new { brands = brands.OrderBy(b => b.Name).ToList() });
    }

    // GET /api/catalog/models?type=refrigerator&brand=Fresh
    [HttpGet("models")]
    public async Task<IActionResult> Models([FromQuery] string? type, [FromQuery] string? brand)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(brand))
            return BadRequest(new { error = "type and brand are required" });
        var items = await _db.CatalogItems.AsNoTracking()
            .Where(c => c.TypeKey == type && c.Brand == brand)
            .OrderBy(c => c.RatedWatts).ToListAsync();
        return Ok(new { models = items.Select(c => new CatalogModelDto(c.Id, c.Model, c.RatedWatts, c.Source)).ToList() });
    }
}
