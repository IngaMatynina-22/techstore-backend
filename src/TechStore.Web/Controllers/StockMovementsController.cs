using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/stock-movements")]
public class StockMovementsController : ApiControllerBase
{
    private readonly IInventoryService _inventory;

    public StockMovementsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] StockMovementQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetMovementsAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetMovementByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("reports/period")]
    public async Task<IActionResult> PeriodReport(
        [FromQuery] int? warehouseId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetMovementsPeriodReportAsync(warehouseId, fromUtc, toUtc, cancellationToken));

    [HttpPost("receipts")]
    public async Task<IActionResult> Receipt([FromBody] ReceiptRequest request, CancellationToken cancellationToken)
        => FromResult(await _inventory.ReceiveAsync(request, CurrentUserId, cancellationToken));

    [HttpPost("issues")]
    public async Task<IActionResult> Issue([FromBody] IssueRequest request, CancellationToken cancellationToken)
        => FromResult(await _inventory.IssueAsync(request, CurrentUserId, cancellationToken));

    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjust([FromBody] AdjustmentRequest request, CancellationToken cancellationToken)
        => FromResult(await _inventory.AdjustAsync(request, CurrentUserId, cancellationToken));

    [HttpPost("transfers")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
        => FromResult(await _inventory.TransferAsync(request, CurrentUserId, cancellationToken));
}
