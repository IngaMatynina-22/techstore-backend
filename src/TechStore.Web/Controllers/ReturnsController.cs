using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Returns;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
[Route("api/returns")]
public class ReturnsController : ApiControllerBase
{
    private readonly IReturnService _returns;

    public ReturnsController(IReturnService returns)
    {
        _returns = returns;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request, CancellationToken cancellationToken)
    {
        var result = await _returns.CreateAsync(request, BuildActor(), cancellationToken);
        if (result.IsSuccess)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
        }

        return FromResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] ReturnQuery query, CancellationToken cancellationToken)
        => Ok(await _returns.GetPagedAsync(query, BuildActor(), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await _returns.GetByIdAsync(id, BuildActor(), cancellationToken);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken)
    {
        var entity = await _returns.GetByIdAsync(id, BuildActor(), cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return Ok(await _returns.GetHistoryAsync(id, BuildActor(), cancellationToken));
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] StaffReturnCommentRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _returns.ApproveAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] StaffReturnCommentRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _returns.RejectAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> MarkReceived(
        int id,
        [FromBody] StaffReturnCommentRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _returns.MarkReceivedAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/apply-inventory")]
    public async Task<IActionResult> ApplyInventory(
        int id,
        [FromBody] StaffReturnCommentRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _returns.ApplyInventoryAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(
        int id,
        [FromBody] StaffReturnCommentRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _returns.CloseAsync(id, BuildActor(), body?.Comment, cancellationToken));

    private ActorContext BuildActor()
    {
        var userId = CurrentUserId
            ?? throw new InvalidOperationException("Authenticated user id is required.");

        return new ActorContext
        {
            UserId = userId,
            IsStaff = User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.Manager),
            IsCustomer = User.IsInRole(ApplicationRoles.Customer)
        };
    }
}
