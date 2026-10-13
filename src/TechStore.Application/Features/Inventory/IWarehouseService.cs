using TechStore.Application.Common;

namespace TechStore.Application.Features.Inventory;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<WarehouseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<Result<WarehouseDto>> UpdateAsync(int id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
