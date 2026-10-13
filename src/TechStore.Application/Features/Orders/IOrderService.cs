using TechStore.Application.Common;

namespace TechStore.Application.Features.Orders;

public interface IOrderService
{
    Task<Result<OrderDetailsDto>> CreateAsync(CreateOrderRequest request, ActorContext actor, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetPagedAsync(OrderQuery query, ActorContext actor, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto?> GetByIdAsync(int id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(int id, ActorContext actor, CancellationToken cancellationToken = default);

    Task<Result<OrderDetailsDto>> ConfirmAsync(int id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailsDto>> StartProcessingAsync(int id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailsDto>> CompleteAsync(int id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailsDto>> CancelAsync(int id, ActorContext actor, string? comment = null, CancellationToken cancellationToken = default);

    Task<Result<OrderPeriodSummaryDto>> GetPeriodSummaryAsync(DateTime fromUtc, DateTime toUtc, int? statusId, ActorContext actor, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrdersByDayDto>>> GetByDayReportAsync(DateTime fromUtc, DateTime toUtc, ActorContext actor, CancellationToken cancellationToken = default);
}
