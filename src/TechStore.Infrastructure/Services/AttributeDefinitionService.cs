using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Attributes;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class AttributeDefinitionService : IAttributeDefinitionService
{
    private readonly ApplicationDbContext _db;

    public AttributeDefinitionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetByCategoryAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AttributeDefinitions
            .AsNoTracking()
            .Where(a => a.CategoryId == categoryId)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .Select(a => new AttributeDefinitionDto(
                a.Id,
                a.CategoryId,
                a.Category.Name,
                a.Name,
                a.Code,
                a.DataType,
                a.Unit,
                a.IsRequired,
                a.IsFilterable,
                a.SortOrder,
                a.Options
                    .OrderBy(o => o.SortOrder)
                    .ThenBy(o => o.Value)
                    .Select(o => new AttributeOptionDto(o.Id, o.Value, o.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<AttributeDefinitionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.AttributeDefinitions
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AttributeDefinitionDto(
                a.Id,
                a.CategoryId,
                a.Category.Name,
                a.Name,
                a.Code,
                a.DataType,
                a.Unit,
                a.IsRequired,
                a.IsFilterable,
                a.SortOrder,
                a.Options
                    .OrderBy(o => o.SortOrder)
                    .ThenBy(o => o.Value)
                    .Select(o => new AttributeOptionDto(o.Id, o.Value, o.SortOrder))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<int>> CreateAsync(
        CreateAttributeDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDefinitionAsync(
            request.CategoryId,
            request.Name,
            request.Code,
            request.DataType,
            request.Options,
            excludeId: null,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<int>(validation.Error!);
        }

        var now = DateTime.UtcNow;
        var entity = new AttributeDefinition
        {
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Code = validation.Value!,
            DataType = request.DataType,
            Unit = NormalizeOptional(request.Unit),
            IsRequired = request.IsRequired,
            IsFilterable = request.IsFilterable,
            SortOrder = request.SortOrder,
            CreatedAt = now
        };

        if (request.DataType == AttributeDataType.Select)
        {
            var sort = 0;
            foreach (var option in request.Options.Where(o => !string.IsNullOrWhiteSpace(o)))
            {
                entity.Options.Add(new AttributeOption
                {
                    Value = option.Trim(),
                    SortOrder = sort++
                });
            }
        }

        _db.AttributeDefinitions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(
        int id,
        UpdateAttributeDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.AttributeDefinitions
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Attribute definition not found.");
        }

        var validation = await ValidateDefinitionAsync(
            entity.CategoryId,
            request.Name,
            request.Code,
            request.DataType,
            request.Options,
            excludeId: id,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        entity.Name = request.Name.Trim();
        entity.Code = validation.Value!;
        entity.DataType = request.DataType;
        entity.Unit = NormalizeOptional(request.Unit);
        entity.IsRequired = request.IsRequired;
        entity.IsFilterable = request.IsFilterable;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        if (request.DataType == AttributeDataType.Select)
        {
            var incoming = request.Options
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toRemove = entity.Options
                .Where(o => !incoming.Contains(o.Value, StringComparer.OrdinalIgnoreCase))
                .ToList();
            _db.AttributeOptions.RemoveRange(toRemove);

            var sort = 0;
            foreach (var value in incoming)
            {
                var existing = entity.Options.FirstOrDefault(o =>
                    o.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    entity.Options.Add(new AttributeOption { Value = value, SortOrder = sort });
                }
                else
                {
                    existing.Value = value;
                    existing.SortOrder = sort;
                }

                sort++;
            }
        }
        else if (entity.Options.Count > 0)
        {
            _db.AttributeOptions.RemoveRange(entity.Options);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<VariantAttributeValueDto>> GetVariantValuesAsync(
        int variantId,
        CancellationToken cancellationToken = default)
    {
        return await _db.VariantAttributeValues
            .AsNoTracking()
            .Where(v => v.VariantId == variantId)
            .OrderBy(v => v.Attribute.SortOrder)
            .ThenBy(v => v.Attribute.Name)
            .Select(v => new VariantAttributeValueDto(
                v.Id,
                v.VariantId,
                v.AttributeId,
                v.Attribute.Name,
                v.Attribute.DataType,
                v.Attribute.Unit,
                v.Value,
                v.OptionId,
                v.Option != null ? v.Option.Value : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> SetVariantValueAsync(
        int variantId,
        SetVariantAttributeValueRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new { v.Id, v.Product.CategoryId })
            .FirstOrDefaultAsync(cancellationToken);
        if (variant is null)
        {
            return Result.Failure("Variant not found.");
        }

        var attribute = await _db.AttributeDefinitions
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == request.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result.Failure("Attribute definition not found.");
        }

        if (attribute.CategoryId != variant.CategoryId)
        {
            return Result.Failure("Attribute does not belong to the product category.");
        }

        var optionIds = attribute.Options.Select(o => o.Id).ToList();
        var validation = AttributeValueValidator.Validate(
            attribute.DataType,
            attribute.IsRequired,
            request.Value,
            request.OptionId,
            optionIds);
        if (validation.IsFailure)
        {
            return validation;
        }

        var existing = await _db.VariantAttributeValues
            .FirstOrDefaultAsync(v => v.VariantId == variantId && v.AttributeId == request.AttributeId, cancellationToken);

        var hasValue = !string.IsNullOrWhiteSpace(request.Value) || request.OptionId.HasValue;
        if (!hasValue)
        {
            if (existing is not null)
            {
                _db.VariantAttributeValues.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        if (existing is null)
        {
            existing = new VariantAttributeValue
            {
                VariantId = variantId,
                AttributeId = request.AttributeId
            };
            _db.VariantAttributeValues.Add(existing);
        }

        if (attribute.DataType == AttributeDataType.Select)
        {
            existing.OptionId = request.OptionId;
            existing.Value = attribute.Options.First(o => o.Id == request.OptionId!.Value).Value;
        }
        else
        {
            existing.OptionId = null;
            existing.Value = NormalizeOptional(request.Value);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<string>> ValidateDefinitionAsync(
        int categoryId,
        string name,
        string? code,
        AttributeDataType dataType,
        IReadOnlyList<string> options,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string>("Attribute name is required.");
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure<string>("Category was not found.");
        }

        if (!Enum.IsDefined(dataType))
        {
            return Result.Failure<string>("Invalid attribute data type.");
        }

        var resolvedCode = string.IsNullOrWhiteSpace(code)
            ? SlugHelper.Generate(name).Replace('-', '_')
            : SlugHelper.Generate(code).Replace('-', '_');

        if (string.IsNullOrWhiteSpace(resolvedCode))
        {
            return Result.Failure<string>("Attribute code is invalid.");
        }

        var codeExists = await _db.AttributeDefinitions.AnyAsync(
            a => a.CategoryId == categoryId &&
                 a.Code == resolvedCode &&
                 (!excludeId.HasValue || a.Id != excludeId.Value),
            cancellationToken);
        if (codeExists)
        {
            return Result.Failure<string>("An attribute with this code already exists in the category.");
        }

        if (dataType == AttributeDataType.Select)
        {
            var cleaned = options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
            if (cleaned.Count == 0)
            {
                return Result.Failure<string>("Select attributes require at least one option.");
            }
        }

        return Result.Success(resolvedCode);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
