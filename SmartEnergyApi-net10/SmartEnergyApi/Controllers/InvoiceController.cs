using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEnergyApi.Data;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

/// <summary>Estimated invoice (weekly / monthly) for all devices or one device.</summary>
[ApiController]
[Route("api/invoice")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InvoiceService _invoices;
    private readonly SimulationService _sim;

    public InvoiceController(AppDbContext db, InvoiceService invoices, SimulationService sim)
    {
        _db = db;
        _invoices = invoices;
        _sim = sim;
    }

    // GET /api/invoice?period=weekly|monthly&deviceId=
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? period, [FromQuery] int? deviceId)
    {
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });
        var inv = await _invoices.BuildAsync(user, period == "monthly" ? "monthly" : "weekly", deviceId);
        if (inv == null) return NotFound(new { error = "Device not found" });
        return Ok(inv);
    }

    // POST /api/invoice/email  { period, deviceId, lang }
    [HttpPost("email")]
    public async Task<IActionResult> Email(EmailInvoiceRequest req)
    {
        if (!_invoices.MailConfigured) return StatusCode(503, new { error = "Email sending is not configured on the server" });
        var userId = User.GetUserId();
        await _sim.TickAsync(userId);
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found" });
        var inv = await _invoices.BuildAsync(user, req.Period == "monthly" ? "monthly" : "weekly", req.DeviceId);
        if (inv == null) return NotFound(new { error = "Device not found" });
        try
        {
            await _invoices.SendEmailAsync(user, inv, req.Lang == "ar" ? "ar" : "en");
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not send the email: " + ex.Message });
        }
        return Ok(new { message = "Invoice sent" });
    }
}
