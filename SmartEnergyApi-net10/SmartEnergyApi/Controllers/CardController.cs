using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEnergyApi.Dtos;
using SmartEnergyApi.Services;

namespace SmartEnergyApi.Controllers;

[ApiController]
[Route("api/card")]
[Authorize]
public class CardController : ControllerBase
{
    private readonly CardService _card;

    public CardController(CardService card)
    {
        _card = card;
    }

    // GET /api/card  (balance + approximate spend per device)
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.GetUserId();
        await _card.CheckTierCrossingAsync(userId);
        await _card.CheckBalanceAlertsAsync(userId);
        return Ok(await _card.GetSummaryAsync(userId));
    }

    // POST /api/card/recharge  { amount }  -> adds to what is left
    [HttpPost("recharge")]
    public async Task<IActionResult> Recharge(CardAmountRequest req)
    {
        if (double.IsNaN(req.Amount) || req.Amount <= 0 || req.Amount > 1_000_000)
            return BadRequest(new { error = "Enter a valid amount" });
        var userId = User.GetUserId();
        await _card.RechargeAsync(userId, req.Amount);
        return Ok(await _card.GetSummaryAsync(userId));
    }

    // PUT /api/card  { amount }  -> sets the balance to exactly this value
    [HttpPut]
    public async Task<IActionResult> SetBalance(CardAmountRequest req)
    {
        if (double.IsNaN(req.Amount) || req.Amount < 0 || req.Amount > 1_000_000)
            return BadRequest(new { error = "Enter a valid amount" });
        var userId = User.GetUserId();
        await _card.SetBalanceAsync(userId, req.Amount);
        return Ok(await _card.GetSummaryAsync(userId));
    }
}
