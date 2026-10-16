using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize]
[Route("api/warehouses")]
public class WarehousesController : ApiControllerBase
{
    private readonly IWarehouseService _warehouses;
    private readonly IInventoryService _inventory;

    public WarehousesController(IWarehouseService warehouses, IInventoryService inventory)
    {
        _warehouses = warehouses;
        _inventory = inventory;
    }

    /// <summary>Customer may list warehouses to pick fulfillment location when placing an order.</summary>
    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
        => Ok(await _warehouses.GetAllAsync(includeInactive, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _warehouses.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await _warehouses.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : FromResult(result);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWarehouseRequest request, CancellationToken cancellationToken)
        => FromResult(await _warehouses.UpdateAsync(id, request, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _warehouses.DeactivateAsync(id, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> Summary(int id, CancellationToken cancellationToken)
    {
        var summary = await _inventory.GetWarehouseSummaryAsync(id, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("{id:int}/valuation")]
    public async Task<IActionResult> Valuation(int id, CancellationToken cancellationToken)
    {
        var valuation = await _inventory.GetInventoryValuationAsync(id, cancellationToken);
        return valuation is null ? NotFound() : Ok(valuation);
    }
}
