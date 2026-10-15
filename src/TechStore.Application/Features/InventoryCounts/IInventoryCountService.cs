using TechStore.Application.Common;

namespace TechStore.Application.Features.InventoryCounts;

public interface IInventoryCountService
{
    Task<Result<InventoryCountSessionDto>> CreateAsync(CreateInventoryCountRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<InventoryCountSessionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<InventoryCountSessionDto>> SetActualsAsync(int id, SetCountActualsRequest request, CancellationToken cancellationToken = default);
    Task<Result<InventoryCountSessionDto>> SubmitForReviewAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<InventoryCountSessionDto>> ApplyAsync(int id, int? userId, CancellationToken cancellationToken = default);
    Task<Result<InventoryCountSessionDto>> CancelAsync(int id, CancellationToken cancellationToken = default);
}
