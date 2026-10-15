using TechStore.Application.Common;
using TechStore.Application.Features.Orders;

namespace TechStore.Application.Features.Returns;

public interface IReturnService
{
    Task<Result<ReturnDetailsDto>> CreateAsync(
        CreateReturnRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReturnSummaryDto>> GetPagedAsync(
        ReturnQuery query,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<ReturnDetailsDto?> GetByIdAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReturnStatusHistoryDto>> GetHistoryAsync(
        int id,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnDetailsDto>> ApproveAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnDetailsDto>> RejectAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnDetailsDto>> MarkReceivedAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnDetailsDto>> ApplyInventoryAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<Result<ReturnDetailsDto>> CloseAsync(
        int id,
        ActorContext actor,
        string? comment = null,
        CancellationToken cancellationToken = default);
}
