using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Inventory;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class WarehouseService : IWarehouseService
{
    private readonly ApplicationDbContext _db;

    public WarehouseService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetAllAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Warehouses.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(w => w.IsActive);
        }

        return await query
            .OrderBy(w => w.Name)
            .Select(w => Map(w))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => Map(w))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<WarehouseDto>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(request.Name, request.Code, excludeId: null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<WarehouseDto>(validation.Error!, validation.Code);
        }

        var entity = new Warehouse
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Address = Normalize(request.Address),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Warehouses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<Result<WarehouseDto>> UpdateAsync(
        int id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound<WarehouseDto>("Warehouse not found.");
        }

        var validation = await ValidateAsync(request.Name, request.Code, excludeId: id, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<WarehouseDto>(validation.Error!, validation.Code);
        }

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Address = Normalize(request.Address);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound("Warehouse not found.");
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

    private async Task<Result> ValidateAsync(string name, string code, int? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Warehouse name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure("Warehouse code is required.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 50)
        {
            return Result.Failure("Warehouse code must be at most 50 characters.");
        }

        var codeExists = await _db.Warehouses.AnyAsync(
            w => w.Code == normalizedCode && (!excludeId.HasValue || w.Id != excludeId.Value),
            cancellationToken);

        return codeExists
            ? Result.Conflict("A warehouse with this code already exists.")
            : Result.Success();
    }

    private static WarehouseDto Map(Warehouse w) =>
        new(w.Id, w.Name, w.Code, w.Address, w.IsActive, w.CreatedAt, w.UpdatedAt);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
