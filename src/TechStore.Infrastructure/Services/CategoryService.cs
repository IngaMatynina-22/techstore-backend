using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Categories;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _db;

    public CategoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetListAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.Slug,
                c.ParentId,
                c.Parent != null ? c.Parent.Name : null,
                c.SortOrder,
                c.IsActive,
                c.Children.Count,
                c.Products.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryLookupDto>> GetLookupAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryLookupDto(c.Id, c.Name, c.Slug, c.ParentId, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.ImageUrl,
                c.ParentId,
                ParentName = c.Parent != null ? c.Parent.Name : null,
                c.SortOrder,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var children = await _db.Categories
            .AsNoTracking()
            .Where(c => c.ParentId == id)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.Slug,
                c.ParentId,
                category.Name,
                c.SortOrder,
                c.IsActive,
                c.Children.Count,
                c.Products.Count))
            .ToListAsync(cancellationToken);

        return new CategoryDetailsDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ImageUrl,
            category.ParentId,
            category.ParentName,
            category.SortOrder,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt,
            children);
    }

    public async Task<Result<int>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(request.Name, request.Slug, request.ParentId, excludeId: null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<int>(validation.Error!);
        }

        var entity = new Category
        {
            Name = request.Name.Trim(),
            Slug = validation.Value!,
            Description = NormalizeOptional(request.Description),
            ImageUrl = NormalizeOptional(request.ImageUrl),
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Category not found.");
        }

        var validation = await ValidateAsync(request.Name, request.Slug, request.ParentId, excludeId: id, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        if (request.ParentId.HasValue)
        {
            var parentMap = await _db.Categories
                .AsNoTracking()
                .Select(c => new { c.Id, c.ParentId })
                .ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);

            if (CategoryHierarchyRules.WouldCreateCycle(
                    id,
                    request.ParentId,
                    pid => parentMap.TryGetValue(pid, out var parentId) ? parentId : null))
            {
                return Result.Failure("Cannot set parent category: this would create a circular hierarchy.");
            }
        }

        entity.Name = request.Name.Trim();
        entity.Slug = validation.Value!;
        entity.Description = NormalizeOptional(request.Description);
        entity.ImageUrl = NormalizeOptional(request.ImageUrl);
        entity.ParentId = request.ParentId;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Category not found.");
        }

        if (!entity.IsActive)
        {
            return Result.Success();
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<string>> ValidateAsync(
        string name,
        string? slug,
        int? parentId,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string>("Category name is required.");
        }

        if (name.Trim().Length > 200)
        {
            return Result.Failure<string>("Category name must be at most 200 characters.");
        }

        var resolvedSlug = string.IsNullOrWhiteSpace(slug)
            ? SlugHelper.Generate(name)
            : SlugHelper.Generate(slug);

        if (!SlugHelper.IsValid(resolvedSlug))
        {
            return Result.Failure<string>("Slug is invalid. Use lowercase letters, numbers and hyphens.");
        }

        if (resolvedSlug.Length > 220)
        {
            return Result.Failure<string>("Slug must be at most 220 characters.");
        }

        var slugExists = await _db.Categories.AnyAsync(
            c => c.Slug == resolvedSlug && (!excludeId.HasValue || c.Id != excludeId.Value),
            cancellationToken);
        if (slugExists)
        {
            return Result.Failure<string>("A category with this slug already exists.");
        }

        if (parentId.HasValue)
        {
            var parentExists = await _db.Categories.AnyAsync(c => c.Id == parentId.Value, cancellationToken);
            if (!parentExists)
            {
                return Result.Failure<string>("Parent category was not found.");
            }
        }

        return Result.Success(resolvedSlug);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
