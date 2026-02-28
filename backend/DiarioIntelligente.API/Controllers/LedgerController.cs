using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LedgerController : AuthenticatedController
{
    private readonly ILedgerQueryService _ledgerQueryService;

    public LedgerController(ILedgerQueryService ledgerQueryService)
    {
        _ledgerQueryService = ledgerQueryService;
    }

    [HttpGet("debts")]
    public async Task<ActionResult<List<OpenDebtResponse>>> GetOpenDebts()
    {
        var debts = await _ledgerQueryService.GetOpenDebtsAsync(GetUserId(), HttpContext.RequestAborted);
        return Ok(debts);
    }

    [HttpGet("debts/{counterparty}")]
    public async Task<ActionResult<OpenDebtResponse>> GetOpenDebtForCounterparty(string counterparty)
    {
        var debt = await _ledgerQueryService.GetOpenDebtForCounterpartyAsync(GetUserId(), counterparty, HttpContext.RequestAborted);
        if (debt == null)
            return NotFound();

        return Ok(debt);
    }

    [HttpGet("spending/my")]
    public async Task<ActionResult<SpendingSummaryResponse>> GetMySpending([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo) = ResolveRange(from, to);
        var response = await _ledgerQueryService.GetMySpendingAsync(GetUserId(), rangeFrom, rangeTo, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("spending/events")]
    public async Task<ActionResult<EventSpendingSummaryResponse>> GetEventSpending(
        [FromQuery] string eventType = "cena",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var (rangeFrom, rangeTo) = ResolveRange(from, to);
        var response = await _ledgerQueryService.GetEventSpendingAsync(
            GetUserId(),
            eventType,
            rangeFrom,
            rangeTo,
            HttpContext.RequestAborted);

        return Ok(response);
    }

    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var rangeFrom = from ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rangeTo = to ?? now;
        return (rangeFrom, rangeTo);
    }
}
