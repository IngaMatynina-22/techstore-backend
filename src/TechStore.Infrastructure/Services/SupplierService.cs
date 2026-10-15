using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Suppliers;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _db;

    public SupplierService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SupplierDto>> GetPagedAsync(
        SupplierQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = NormalizePage(query.Page);
        var pageSize = NormalizePageSize(query.PageSize);

        var suppliers = _db.Suppliers.AsNoTracking().AsQueryable();

        if (!query.IncludeInactive)
        {
            suppliers = suppliers.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            suppliers = suppliers.Where(s =>
                s.Name.Contains(search) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(search)) ||
                (s.Email != null && s.Email.Contains(search)) ||
                (s.Phone != null && s.Phone.Contains(search)) ||
                (s.TaxNumber != null && s.TaxNumber.Contains(search)));
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => Map(s))
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => Map(s))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<SupplierDto>> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateNameAsync(request.Name, excludeId: null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<SupplierDto>(validation.Error!, validation.Code);
        }

        var entity = new Supplier
        {
            Name = request.Name.Trim(),
            ContactPerson = Normalize(request.ContactPerson),
            Email = Normalize(request.Email),
            Phone = Normalize(request.Phone),
            Address = Normalize(request.Address),
            TaxNumber = Normalize(request.TaxNumber),
            Notes = Normalize(request.Notes),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(null, "Supplier", entity.Id.ToString(), AuditAction.Create,
            $"Created supplier '{entity.Name}'", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(entity));
    }

    public async Task<Result<SupplierDto>> UpdateAsync(
        int id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound<SupplierDto>("Supplier not found.");
        }

        var validation = await ValidateNameAsync(request.Name, excludeId: id, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<SupplierDto>(validation.Error!, validation.Code);
        }

        entity.Name = request.Name.Trim();
        entity.ContactPerson = Normalize(request.ContactPerson);
        entity.Email = Normalize(request.Email);
        entity.Phone = Normalize(request.Phone);
        entity.Address = Normalize(request.Address);
        entity.TaxNumber = Normalize(request.TaxNumber);
        entity.Notes = Normalize(request.Notes);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync(null, "Supplier", entity.Id.ToString(), AuditAction.Update,
            $"Updated supplier '{entity.Name}'", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound("Supplier not found.");
        }

        if (!entity.IsActive)
        {
            return Result.Success();
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await WriteAuditAsync(null, "Supplier", entity.Id.ToString(), AuditAction.Deactivate,
            $"Deactivated supplier '{entity.Name}'", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<SupplierProductDto>> GetProductsAsync(
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Suppliers.AsNoTracking().AnyAsync(s => s.Id == supplierId, cancellationToken);
        if (!exists)
        {
            return [];
        }

        return await _db.SupplierProducts
            .AsNoTracking()
            .Where(sp => sp.SupplierId == supplierId)
            .OrderBy(sp => sp.Variant.Sku)
            .Select(sp => new SupplierProductDto(
                sp.Id,
                sp.SupplierId,
                sp.VariantId,
                sp.Variant.Sku,
                sp.Variant.VariantName,
                sp.Variant.Product.Name,
                sp.SupplierSku,
                sp.SupplyPrice,
                sp.DeliveryDays,
                sp.IsPreferred))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<SupplierProductDto>> UpsertSupplierProductAsync(
        int supplierId,
        UpsertSupplierProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.NotFound<SupplierProductDto>("Supplier not found.");
        }

        if (request.SupplyPrice < 0)
        {
            return Result.Failure<SupplierProductDto>("Supply price cannot be negative.");
        }

        if (request.DeliveryDays is < 0)
        {
            return Result.Failure<SupplierProductDto>("Delivery days cannot be negative.");
        }

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken);
        if (variant is null)
        {
            return Result.NotFound<SupplierProductDto>("Product variant not found.");
        }

        var existing = await _db.SupplierProducts
            .FirstOrDefaultAsync(
                sp => sp.SupplierId == supplierId && sp.VariantId == request.VariantId,
                cancellationToken);

        if (existing is null)
        {
            existing = new SupplierProduct
            {
                SupplierId = supplierId,
                VariantId = request.VariantId
            };
            _db.SupplierProducts.Add(existing);
        }

        existing.SupplierSku = Normalize(request.SupplierSku);
        existing.SupplyPrice = request.SupplyPrice;
        existing.DeliveryDays = request.DeliveryDays;
        existing.IsPreferred = request.IsPreferred;

        await WriteAuditAsync(null, "SupplierProduct", supplierId.ToString(), AuditAction.Update,
            $"Upserted variant {request.VariantId} supplyPrice={request.SupplyPrice}", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new SupplierProductDto(
            existing.Id,
            existing.SupplierId,
            existing.VariantId,
            variant.Sku,
            variant.VariantName,
            variant.Product.Name,
            existing.SupplierSku,
            existing.SupplyPrice,
            existing.DeliveryDays,
            existing.IsPreferred));
    }

    private async Task<Result> ValidateNameAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Supplier name is required.");
        }

        if (name.Trim().Length > 200)
        {
            return Result.Failure("Supplier name must be at most 200 characters.");
        }

        var normalized = name.Trim().ToLower();
        var exists = await _db.Suppliers.AnyAsync(
            s => s.Name.ToLower() == normalized && (!excludeId.HasValue || s.Id != excludeId.Value),
            cancellationToken);

        return exists
            ? Result.Conflict("A supplier with this name already exists.")
            : Result.Success();
    }

    private async Task WriteAuditAsync(
        int? userId,
        string entityType,
        string entityId,
        AuditAction action,
        string details,
        CancellationToken cancellationToken)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            NewValues = details,
            CreatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    private static SupplierDto Map(Supplier s) =>
        new(s.Id, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.TaxNumber, s.Notes,
            s.IsActive, s.CreatedAt, s.UpdatedAt);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize) => pageSize is < 1 or > 100 ? 20 : pageSize;
}
