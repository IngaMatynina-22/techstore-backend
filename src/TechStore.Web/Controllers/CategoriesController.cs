using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Categories;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize]
[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
        => Ok(await _categories.GetListAsync(includeInactive, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _categories.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categories.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : FromResult(result);
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
        => FromResult(await _categories.UpdateAsync(id, request, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
        => FromResult(await _categories.DeactivateAsync(id, cancellationToken));
}
