using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize]
[Route("api/inventory")]
public class InventoryController : ApiControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    /// <summary>Customer may read availability to pick a warehouse when placing an order.</summary>
    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetPagedAsync(query, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
    [HttpGet("{warehouseId:int}/{variantId:int}")]
    public async Task<IActionResult> Get(int warehouseId, int variantId, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetAsync(warehouseId, variantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetLowStockAsync(query, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("out-of-stock")]
    public async Task<IActionResult> OutOfStock([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetOutOfStockAsync(query, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPut("variants/{variantId:int}/low-stock-threshold")]
    public async Task<IActionResult> SetThreshold(
        int variantId,
        [FromBody] SetLowStockThresholdRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _inventory.SetLowStockThresholdAsync(variantId, request, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("integrity-report")]
    public async Task<IActionResult> IntegrityReport(CancellationToken cancellationToken)
        => Ok(await _inventory.GetIntegrityReportAsync(cancellationToken));
}
