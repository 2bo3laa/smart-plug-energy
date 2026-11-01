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
    private readonly SimulationService _sim;

    public StatisticsController(StatisticsService stats, SimulationService sim)
    {
        _stats = stats;
        _sim = sim;
    }

    // GET /api/statistics?range=day|week|month|year
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? range)
    {
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);
        var result = await _stats.BuildAsync(userId, range ?? "day");
        return Ok(result);
    }
}
