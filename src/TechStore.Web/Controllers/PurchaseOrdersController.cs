using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Purchasing;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ApiControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrders)
    {
        _purchaseOrders = purchaseOrders;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PurchaseOrderQuery query, CancellationToken cancellationToken)
        => Ok(await _purchaseOrders.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await _purchaseOrders.GetByIdAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken)
    {
        var order = await _purchaseOrders.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(await _purchaseOrders.GetHistoryAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrders.CreateDraftAsync(request, BuildActor(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : FromResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.UpdateDraftAsync(id, request, BuildActor(), cancellationToken));

    [HttpPut("{id:int}/items")]
    public async Task<IActionResult> SetItems(
        int id,
        [FromBody] SetPurchaseOrderItemsRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.SetItemsAsync(id, request, BuildActor(), cancellationToken));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.ConfirmAsync(id, BuildActor(), cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelPurchaseOrderRequest? body,
        CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.CancelAsync(id, BuildActor(), body?.Comment, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelViaDelete(int id, CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.CancelAsync(id, BuildActor(), null, cancellationToken));

    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> Receive(
        int id,
        [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _purchaseOrders.ReceiveAsync(id, request, BuildActor(), cancellationToken));

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
