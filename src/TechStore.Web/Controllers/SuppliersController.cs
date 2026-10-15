using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Suppliers;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/suppliers")]
public class SuppliersController : ApiControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers)
    {
        _suppliers = suppliers;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] SupplierQuery query, CancellationToken cancellationToken)
        => Ok(await _suppliers.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _suppliers.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _suppliers.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
        => FromResult(await _suppliers.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _suppliers.DeactivateAsync(id, cancellationToken));

    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        return Ok(await _suppliers.GetProductsAsync(id, cancellationToken));
    }

    [HttpPut("{id:int}/products")]
    public async Task<IActionResult> UpsertProduct(
        int id,
        [FromBody] UpsertSupplierProductRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _suppliers.UpsertSupplierProductAsync(id, request, cancellationToken));
}
