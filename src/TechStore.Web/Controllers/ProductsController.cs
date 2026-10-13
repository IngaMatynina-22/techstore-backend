using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Products;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/products")]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ProductQuery query, CancellationToken cancellationToken)
        => Ok(await _products.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _products.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _products.CreateAsync(request, CurrentUserId, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
        => FromResult(await _products.UpdateAsync(id, request, CurrentUserId, cancellationToken));

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _products.DeactivateAsync(id, CurrentUserId, cancellationToken));

    [HttpPost("{id:int}/variants")]
    public async Task<IActionResult> AddVariant(int id, [FromBody] ProductVariantInput request, CancellationToken cancellationToken)
    {
        var result = await _products.AddVariantAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(new { id = result.Value })
            : FromResult(result);
    }

    [HttpPut("{id:int}/variants/{variantId:int}")]
    public async Task<IActionResult> UpdateVariant(
        int id,
        int variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _products.UpdateVariantAsync(id, variantId, request, cancellationToken));
}
