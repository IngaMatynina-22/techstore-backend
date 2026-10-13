using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Brands;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class BrandService : IBrandService
{
    private readonly ApplicationDbContext _db;

    public BrandService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BrandListItemDto>> GetListAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Brands.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandListItemDto(
                b.Id,
                b.Name,
                b.WebsiteUrl,
                b.IsActive,
                b.Products.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BrandLookupDto>> GetLookupAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Brands.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(b => b.IsActive);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandLookupDto(b.Id, b.Name, b.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<BrandDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BrandDetailsDto(
                b.Id,
                b.Name,
                b.Description,
                b.LogoUrl,
                b.WebsiteUrl,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                b.Products.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<int>> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(request.Name, excludeId: null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<int>(validation.Error!);
        }

        var entity = new Brand
        {
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            LogoUrl = NormalizeOptional(request.LogoUrl),
            WebsiteUrl = NormalizeOptional(request.WebsiteUrl),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Brands.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Brand not found.");
        }

        var validation = await ValidateAsync(request.Name, excludeId: id, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.LogoUrl = NormalizeOptional(request.LogoUrl);
        entity.WebsiteUrl = NormalizeOptional(request.WebsiteUrl);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Brand not found.");
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

    private async Task<Result> ValidateAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Brand name is required.");
        }

        if (name.Trim().Length > 200)
        {
            return Result.Failure("Brand name must be at most 200 characters.");
        }

        var normalized = name.Trim();
        var exists = await _db.Brands.AnyAsync(
            b => b.Name == normalized && (!excludeId.HasValue || b.Id != excludeId.Value),
            cancellationToken);

        return exists
            ? Result.Failure("A brand with this name already exists.")
            : Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
