using TechStore.Application.Common;

namespace TechStore.Application.Features.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(SupplierQuery query, CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<Result<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierProductDto>> GetProductsAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<Result<SupplierProductDto>> UpsertSupplierProductAsync(
        int supplierId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default);
}
