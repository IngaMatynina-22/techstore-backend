using TechStore.Application.Common;
using TechStore.Application.Features.Orders;

namespace TechStore.Application.Features.Purchasing;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderSummaryDto>> GetPagedAsync(
        PurchaseOrderQuery query,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrderStatusHistoryDto>> GetHistoryAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(
        CreatePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(
        int id,
        UpdatePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> SetItemsAsync(
        int id,
        SetPurchaseOrderItemsRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> ConfirmAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> CancelAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> ReceiveAsync(
        int id,
        ReceivePurchaseOrderRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);
}
