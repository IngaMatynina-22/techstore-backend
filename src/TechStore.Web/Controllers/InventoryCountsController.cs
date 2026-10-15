using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.InventoryCounts;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/inventory-counts")]
public class InventoryCountsController : ApiControllerBase
{
    private readonly IInventoryCountService _counts;

    public InventoryCountsController(IInventoryCountService counts)
    {
        _counts = counts;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryCountRequest request, CancellationToken cancellationToken)
    {
        var result = await _counts.CreateAsync(request, CurrentUserId, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _counts.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id:int}/actuals")]
    public async Task<IActionResult> SetActuals(int id, [FromBody] SetCountActualsRequest request, CancellationToken cancellationToken)
        => FromResult(await _counts.SetActualsAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/submit-review")]
    public async Task<IActionResult> SubmitReview(int id, CancellationToken cancellationToken)
        => FromResult(await _counts.SubmitForReviewAsync(id, cancellationToken));

    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, CancellationToken cancellationToken)
        => FromResult(await _counts.ApplyAsync(id, CurrentUserId, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
        => FromResult(await _counts.CancelAsync(id, cancellationToken));
}
