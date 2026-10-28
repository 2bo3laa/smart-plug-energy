using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });

        var deviceCount = await _db.Devices.CountAsync(d => d.UserId == userId);
        var scheduleCount = await _db.Schedules
            .Join(_db.Devices, s => s.DeviceId, d => d.Id, (s, d) => new { s, d })
            .CountAsync(x => x.d.UserId == userId && x.s.IsActive);

        return Ok(new ProfileResponse(
            new PublicUserDto(user.Id, user.Name, user.Email),
            new ProfileStatsDto(deviceCount, scheduleCount)
        ));
    }

    // PATCH /api/profile
    [HttpPatch]
    public async Task<IActionResult> Update(UpdateProfileRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "name is required" });

        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });

        user.Name = req.Name.Trim();
        await _db.SaveChangesAsync();

        return Ok(new { user = new PublicUserDto(user.Id, user.Name, user.Email) });
    }
}
