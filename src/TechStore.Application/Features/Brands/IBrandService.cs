using TechStore.Application.Common;

namespace TechStore.Application.Features.Brands;

public interface IBrandService
{
    Task<IReadOnlyList<BrandListItemDto>> GetListAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrandLookupDto>> GetLookupAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<BrandDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<int>> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
