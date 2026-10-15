using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Reports;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;

    public ReportsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("sales/summary")]
    public async Task<IActionResult> SalesSummary(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => FromResult(await _analytics.GetSalesSummaryAsync(fromUtc, toUtc, cancellationToken));

    [HttpGet("inventory/overview")]
    public async Task<IActionResult> InventoryOverview(
        [FromQuery] int? warehouseId,
        CancellationToken cancellationToken)
        => FromResult(await _analytics.GetInventoryOverviewAsync(warehouseId, cancellationToken));

    [HttpGet("purchasing/summary")]
    public async Task<IActionResult> PurchasingSummary(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => FromResult(await _analytics.GetPurchasingSummaryAsync(fromUtc, toUtc, cancellationToken));
}
