using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsService _stats;

    public StatisticsController(StatisticsService stats)
    {
        _stats = stats;
    }

    // GET /api/statistics?range=day|week|month|year
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? range)
    {
        var userId = User.GetUserId();
        var result = await _stats.BuildAsync(userId, range ?? "day");
        return Ok(result);
    }
}
