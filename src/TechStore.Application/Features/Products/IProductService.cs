using TechStore.Application.Common;

namespace TechStore.Application.Features.Products;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<int>> CreateAsync(CreateProductRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int id, UpdateProductRequest request, int? userId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(int id, int? userId, CancellationToken cancellationToken = default);
    Task<Result<int>> AddVariantAsync(int productId, ProductVariantInput request, CancellationToken cancellationToken = default);
    Task<Result> UpdateVariantAsync(int productId, int variantId, UpdateProductVariantRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateVariantAsync(int productId, int variantId, CancellationToken cancellationToken = default);
}
