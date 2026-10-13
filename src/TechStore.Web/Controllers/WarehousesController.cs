using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
        => Ok(await _warehouses.GetAllAsync(includeInactive, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _warehouses.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await _warehouses.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWarehouseRequest request, CancellationToken cancellationToken)
        => FromResult(await _warehouses.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _warehouses.DeactivateAsync(id, cancellationToken));

    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> Summary(int id, CancellationToken cancellationToken)
    {
        var summary = await _inventory.GetWarehouseSummaryAsync(id, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:int}/valuation")]
    public async Task<IActionResult> Valuation(int id, CancellationToken cancellationToken)
    {
        var valuation = await _inventory.GetInventoryValuationAsync(id, cancellationToken);
        return valuation is null ? NotFound() : Ok(valuation);
    }
}
