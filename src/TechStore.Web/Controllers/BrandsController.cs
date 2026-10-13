using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Brands;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/brands")]
public class BrandsController : ApiControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands)
    {
        _brands = brands;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
        => Ok(await _brands.GetListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _brands.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await _brands.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBrandRequest request, CancellationToken cancellationToken)
        => FromResult(await _brands.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _brands.DeactivateAsync(id, cancellationToken));
}
