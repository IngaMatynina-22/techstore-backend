using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/inventory")]
public class InventoryController : ApiControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetPagedAsync(query, cancellationToken));

    [HttpGet("{warehouseId:int}/{variantId:int}")]
    public async Task<IActionResult> Get(int warehouseId, int variantId, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetAsync(warehouseId, variantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetLowStockAsync(query, cancellationToken));

    [HttpGet("out-of-stock")]
    public async Task<IActionResult> OutOfStock([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
        => Ok(await _inventory.GetOutOfStockAsync(query, cancellationToken));

    [HttpPut("variants/{variantId:int}/low-stock-threshold")]
    public async Task<IActionResult> SetThreshold(
        int variantId,
        [FromBody] SetLowStockThresholdRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _inventory.SetLowStockThresholdAsync(variantId, request, cancellationToken));
}
