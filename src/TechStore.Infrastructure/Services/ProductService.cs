using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Products;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;

    public ProductService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var products = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(p =>
                p.Name.Contains(search) ||
                p.Variants.Any(v => v.Sku.Contains(search)));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue)
        {
            products = products.Where(p => p.BrandId == query.BrandId.Value);
        }

        if (query.IsActive.HasValue)
        {
            products = products.Where(p => p.IsActive == query.IsActive.Value);
        }

        products = query.SortBy switch
        {
            ProductSortBy.NameAsc => products.OrderBy(p => p.Name),
            ProductSortBy.NameDesc => products.OrderByDescending(p => p.Name),
            ProductSortBy.PriceAsc => products
                .OrderBy(p => p.Variants.Where(v => v.IsActive).Min(v => (decimal?)v.SellingPrice) ?? decimal.MaxValue)
                .ThenBy(p => p.Name),
            ProductSortBy.PriceDesc => products
                .OrderByDescending(p => p.Variants.Where(v => v.IsActive).Max(v => (decimal?)v.SellingPrice) ?? decimal.MinValue)
                .ThenBy(p => p.Name),
            ProductSortBy.CreatedAtAsc => products.OrderBy(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };

        var totalCount = await products.CountAsync(cancellationToken);

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Category.Name,
                p.Brand != null ? p.Brand.Name : null,
                p.Variants.OrderBy(v => v.Id).Select(v => v.Sku).FirstOrDefault(),
                p.Variants.Where(v => v.IsActive).Min(v => (decimal?)v.SellingPrice),
                p.Variants.Where(v => v.IsActive).Max(v => (decimal?)v.SellingPrice),
                p.IsActive,
                p.CreatedAt,
                p.Variants.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailsDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.ShortDescription,
                p.ImageUrl,
                p.WarrantyMonths,
                p.CategoryId,
                p.Category.Name,
                p.BrandId,
                p.Brand != null ? p.Brand.Name : null,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt,
                p.Variants
                    .OrderBy(v => v.Id)
                    .Select(v => new ProductVariantDto(
                        v.Id,
                        v.Sku,
                        v.Barcode,
                        v.VariantName,
                        v.PurchasePrice,
                        v.SellingPrice,
                        v.OldPrice,
                        v.Weight,
                        v.IsActive))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<int>> CreateAsync(
        CreateProductRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var productValidation = await ValidateProductAsync(
            request.Name,
            request.Slug,
            request.CategoryId,
            request.BrandId,
            request.WarrantyMonths,
            excludeId: null,
            cancellationToken);
        if (productValidation.IsFailure)
        {
            return Result.Failure<int>(productValidation.Error!);
        }

        if (request.Variants is null || request.Variants.Count == 0)
        {
            return Result.Failure<int>("At least one product variant is required.");
        }

        foreach (var variant in request.Variants)
        {
            var variantValidation = await ValidateVariantAsync(variant, excludeVariantId: null, cancellationToken);
            if (variantValidation.IsFailure)
            {
                return Result.Failure<int>(variantValidation.Error!);
            }
        }

        var skuGroups = request.Variants
            .Select(v => v.Sku.Trim().ToUpperInvariant())
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (skuGroups.Count > 0)
        {
            return Result.Failure<int>("Duplicate SKUs in the request are not allowed.");
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = productValidation.Value!,
            Description = NormalizeOptional(request.Description),
            ShortDescription = NormalizeOptional(request.ShortDescription),
            ImageUrl = NormalizeOptional(request.ImageUrl),
            WarrantyMonths = request.WarrantyMonths,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            IsActive = request.IsActive,
            CreatedBy = userId,
            CreatedAt = now
        };

        foreach (var input in request.Variants)
        {
            product.Variants.Add(new ProductVariant
            {
                Sku = input.Sku.Trim(),
                Barcode = NormalizeOptional(input.Barcode),
                VariantName = string.IsNullOrWhiteSpace(input.VariantName) ? "Default" : input.VariantName.Trim(),
                PurchasePrice = input.PurchasePrice,
                SellingPrice = input.SellingPrice,
                OldPrice = input.OldPrice,
                Weight = input.Weight,
                IsActive = input.IsActive,
                CreatedAt = now
            });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(product.Id);
    }

    public async Task<Result> UpdateAsync(
        int id,
        UpdateProductRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure("Product not found.");
        }

        var validation = await ValidateProductAsync(
            request.Name,
            request.Slug,
            request.CategoryId,
            request.BrandId,
            request.WarrantyMonths,
            excludeId: id,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        product.Name = request.Name.Trim();
        product.Slug = validation.Value!;
        product.Description = NormalizeOptional(request.Description);
        product.ShortDescription = NormalizeOptional(request.ShortDescription);
        product.ImageUrl = NormalizeOptional(request.ImageUrl);
        product.WarrantyMonths = request.WarrantyMonths;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.IsActive = request.IsActive;
        product.UpdatedBy = userId;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, int? userId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return Result.Failure("Product not found.");
        }

        if (!product.IsActive)
        {
            return Result.Success();
        }

        product.IsActive = false;
        product.UpdatedBy = userId;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<int>> AddVariantAsync(
        int productId,
        ProductVariantInput request,
        CancellationToken cancellationToken = default)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
        {
            return Result.Failure<int>("Product not found.");
        }

        var validation = await ValidateVariantAsync(request, excludeVariantId: null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<int>(validation.Error!);
        }

        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = request.Sku.Trim(),
            Barcode = NormalizeOptional(request.Barcode),
            VariantName = string.IsNullOrWhiteSpace(request.VariantName) ? "Default" : request.VariantName.Trim(),
            PurchasePrice = request.PurchasePrice,
            SellingPrice = request.SellingPrice,
            OldPrice = request.OldPrice,
            Weight = request.Weight,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(variant.Id);
    }

    public async Task<Result> UpdateVariantAsync(
        int productId,
        int variantId,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, cancellationToken);
        if (variant is null)
        {
            return Result.Failure("Variant not found.");
        }

        var input = new ProductVariantInput
        {
            Sku = request.Sku,
            Barcode = request.Barcode,
            VariantName = request.VariantName,
            PurchasePrice = request.PurchasePrice,
            SellingPrice = request.SellingPrice,
            OldPrice = request.OldPrice,
            Weight = request.Weight,
            IsActive = request.IsActive
        };

        var validation = await ValidateVariantAsync(input, excludeVariantId: variantId, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        variant.Sku = request.Sku.Trim();
        variant.Barcode = NormalizeOptional(request.Barcode);
        variant.VariantName = string.IsNullOrWhiteSpace(request.VariantName) ? "Default" : request.VariantName.Trim();
        variant.PurchasePrice = request.PurchasePrice;
        variant.SellingPrice = request.SellingPrice;
        variant.OldPrice = request.OldPrice;
        variant.Weight = request.Weight;
        variant.IsActive = request.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateVariantAsync(
        int productId,
        int variantId,
        CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, cancellationToken);
        if (variant is null)
        {
            return Result.Failure("Variant not found.");
        }

        var activeCount = await _db.ProductVariants
            .CountAsync(v => v.ProductId == productId && v.IsActive && v.Id != variantId, cancellationToken);
        if (variant.IsActive && activeCount == 0)
        {
            return Result.Failure("At least one active variant must remain.");
        }

        variant.IsActive = false;
        variant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<string>> ValidateProductAsync(
        string name,
        string? slug,
        int categoryId,
        int? brandId,
        int? warrantyMonths,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string>("Product name is required.");
        }

        if (name.Trim().Length > 300)
        {
            return Result.Failure<string>("Product name must be at most 300 characters.");
        }

        if (warrantyMonths is < 0)
        {
            return Result.Failure<string>("Warranty months cannot be negative.");
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure<string>("Category was not found.");
        }

        if (brandId.HasValue)
        {
            var brandExists = await _db.Brands.AnyAsync(b => b.Id == brandId.Value, cancellationToken);
            if (!brandExists)
            {
                return Result.Failure<string>("Brand was not found.");
            }
        }

        var resolvedSlug = string.IsNullOrWhiteSpace(slug)
            ? SlugHelper.Generate(name)
            : SlugHelper.Generate(slug);

        if (!SlugHelper.IsValid(resolvedSlug))
        {
            return Result.Failure<string>("Slug is invalid. Use lowercase letters, numbers and hyphens.");
        }

        var slugExists = await _db.Products.AnyAsync(
            p => p.Slug == resolvedSlug && (!excludeId.HasValue || p.Id != excludeId.Value),
            cancellationToken);
        if (slugExists)
        {
            return Result.Failure<string>("A product with this slug already exists.");
        }

        return Result.Success(resolvedSlug);
    }

    private async Task<Result> ValidateVariantAsync(
        ProductVariantInput input,
        int? excludeVariantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Sku))
        {
            return Result.Failure("SKU is required.");
        }

        var sku = input.Sku.Trim();
        if (sku.Length > 100)
        {
            return Result.Failure("SKU must be at most 100 characters.");
        }

        if (input.PurchasePrice < 0 || input.SellingPrice < 0)
        {
            return Result.Failure("Prices cannot be negative.");
        }

        if (input.OldPrice is < 0)
        {
            return Result.Failure("Old price cannot be negative.");
        }

        if (input.Weight is < 0)
        {
            return Result.Failure("Weight cannot be negative.");
        }

        var skuExists = await _db.ProductVariants.AnyAsync(
            v => v.Sku == sku && (!excludeVariantId.HasValue || v.Id != excludeVariantId.Value),
            cancellationToken);
        if (skuExists)
        {
            return Result.Failure($"SKU '{sku}' is already in use.");
        }

        var barcode = NormalizeOptional(input.Barcode);
        if (barcode is not null)
        {
            if (barcode.Length > 100)
            {
                return Result.Failure("Barcode must be at most 100 characters.");
            }

            var barcodeExists = await _db.ProductVariants.AnyAsync(
                v => v.Barcode == barcode && (!excludeVariantId.HasValue || v.Id != excludeVariantId.Value),
                cancellationToken);
            if (barcodeExists)
            {
                return Result.Failure($"Barcode '{barcode}' is already in use.");
            }
        }

        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
