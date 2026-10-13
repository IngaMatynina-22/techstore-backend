using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Attributes;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/attributes")]
public class AttributesController : ApiControllerBase
{
    private readonly IAttributeDefinitionService _attributes;

    public AttributesController(IAttributeDefinitionService attributes)
    {
        _attributes = attributes;
    }

    [HttpGet]
    public async Task<IActionResult> GetByCategory([FromQuery] int categoryId, CancellationToken cancellationToken)
        => Ok(await _attributes.GetByCategoryAsync(categoryId, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _attributes.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAttributeDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _attributes.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAttributeDefinitionRequest request, CancellationToken cancellationToken)
        => FromResult(await _attributes.UpdateAsync(id, request, cancellationToken));

    [HttpGet("variants/{variantId:int}/values")]
    public async Task<IActionResult> GetVariantValues(int variantId, CancellationToken cancellationToken)
        => Ok(await _attributes.GetVariantValuesAsync(variantId, cancellationToken));

    [HttpPut("variants/{variantId:int}/values")]
    public async Task<IActionResult> SetVariantValue(
        int variantId,
        [FromBody] SetVariantAttributeValueRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _attributes.SetVariantValueAsync(variantId, request, cancellationToken));
}
