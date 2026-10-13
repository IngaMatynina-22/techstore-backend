using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Orders;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager},{ApplicationRoles.Customer}")]
[Route("api/orders")]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orders.CreateAsync(request, BuildActor(), cancellationToken);
        if (result.IsSuccess)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
        }

        return FromResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] OrderQuery query, CancellationToken cancellationToken)
        => Ok(await _orders.GetPagedAsync(query, BuildActor(), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, BuildActor(), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("{id:int}/items")]
    public async Task<IActionResult> GetItems(int id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, BuildActor(), cancellationToken);
        return order is null ? NotFound() : Ok(order.Items);
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, BuildActor(), cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(await _orders.GetHistoryAsync(id, BuildActor(), cancellationToken));
    }

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
        => FromResult(await _orders.ConfirmAsync(id, BuildActor(), cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/start-processing")]
    public async Task<IActionResult> StartProcessing(int id, CancellationToken cancellationToken)
        => FromResult(await _orders.StartProcessingAsync(id, BuildActor(), cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
        => FromResult(await _orders.CompleteAsync(id, BuildActor(), cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderRequest? body, CancellationToken cancellationToken)
        => FromResult(await _orders.CancelAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("reports/summary")]
    public async Task<IActionResult> PeriodSummary(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] int? statusId,
        CancellationToken cancellationToken)
        => FromResult(await _orders.GetPeriodSummaryAsync(fromUtc, toUtc, statusId, BuildActor(), cancellationToken));

    [Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
    [HttpGet("reports/by-period")]
    public async Task<IActionResult> ByPeriod(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
        => FromResult(await _orders.GetByDayReportAsync(fromUtc, toUtc, BuildActor(), cancellationToken));

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

public class CancelOrderRequest
{
    public string? Comment { get; set; }
}
