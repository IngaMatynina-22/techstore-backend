using TechStore.Application.Common;

namespace TechStore.Application.Features.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryListItemDto>> GetListAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryLookupDto>> GetLookupAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<CategoryDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<int>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
