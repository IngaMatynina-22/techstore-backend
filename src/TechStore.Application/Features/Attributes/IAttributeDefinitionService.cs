using TechStore.Application.Common;

namespace TechStore.Application.Features.Attributes;

public interface IAttributeDefinitionService
{
    Task<IReadOnlyList<AttributeDefinitionDto>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<AttributeDefinitionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<int>> CreateAsync(CreateAttributeDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int id, UpdateAttributeDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VariantAttributeValueDto>> GetVariantValuesAsync(int variantId, CancellationToken cancellationToken = default);
    Task<Result> SetVariantValueAsync(int variantId, SetVariantAttributeValueRequest request, CancellationToken cancellationToken = default);
}
