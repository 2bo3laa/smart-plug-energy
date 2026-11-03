using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

/// <summary>Called by the scheduled GitHub workflow (header X-Cron-Key, same key as the reports job).</summary>
[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly MaintenanceService _maintenance;
    private readonly IConfiguration _config;

    public MaintenanceController(MaintenanceService maintenance, IConfiguration config)
    {
        _maintenance = maintenance;
        _config = config;
    }

    // POST /api/maintenance/run
    [HttpPost("run")]
    [AllowAnonymous]
    public async Task<IActionResult> Run([FromHeader(Name = "X-Cron-Key")] string? key)
    {
        var expected = _config["Reports:CronKey"];
        if (string.IsNullOrEmpty(expected)) return StatusCode(503, new { error = "Reports:CronKey is not set on the server" });
        if (key != expected) return Unauthorized(new { error = "Invalid key" });
        var (purged, backedUp) = await _maintenance.RunAsync();
        return Ok(new { purged, backedUp });
    }
}
