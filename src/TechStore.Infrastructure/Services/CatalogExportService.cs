using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Exports;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class CatalogExportService : ICatalogExportService
{
    private readonly ApplicationDbContext _db;

    public CatalogExportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CsvExportFile>> ExportProductsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProductVariants
            .AsNoTracking()
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Sku)
            .Select(v => new
            {
                v.Sku,
                ProductName = v.Product.Name,
                v.VariantName,
                CategorySlug = v.Product.Category.Slug,
                BrandName = v.Product.Brand != null ? v.Product.Brand.Name : null,
                v.PurchasePrice,
                v.SellingPrice,
                v.Barcode,
                IsActive = v.IsActive && v.Product.IsActive
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Sku,ProductName,VariantName,CategorySlug,BrandName,PurchasePrice,SellingPrice,Barcode,IsActive");
        foreach (var row in rows)
        {
            sb.Append(CsvUtil.EscapeAndSanitize(row.Sku)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.ProductName)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.VariantName)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.CategorySlug)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.BrandName)).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.PurchasePrice))).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.SellingPrice))).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Barcode)).Append(',');
            sb.AppendLine(CsvUtil.Escape(CsvUtil.FormatBool(row.IsActive)));
        }

        return Result.Success(new CsvExportFile(
            "products.csv",
            "text/csv",
            CsvUtil.ToUtf8Bytes(sb)));
    }

    public async Task<Result<CsvExportFile>> ExportSuppliersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from supplier in _db.Suppliers.AsNoTracking()
            from product in _db.SupplierProducts.AsNoTracking()
                .Where(item => item.SupplierId == supplier.Id)
                .DefaultIfEmpty()
            orderby supplier.Name, supplier.Id, product == null ? string.Empty : product.Variant.Sku
            select new
            {
                supplier.Id,
                supplier.Name,
                supplier.ContactPerson,
                supplier.Email,
                supplier.Phone,
                supplier.Address,
                supplier.TaxNumber,
                supplier.Notes,
                supplier.IsActive,
                ProductSku = product == null ? null : product.Variant.Sku,
                SupplierSku = product == null ? null : product.SupplierSku,
                SupplyPrice = product == null ? (decimal?)null : product.SupplyPrice,
                DeliveryDays = product == null ? (int?)null : product.DeliveryDays,
                IsPreferred = product != null && product.IsPreferred
            }).ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("SupplierId,SupplierName,ContactPerson,Email,Phone,Address,TaxNumber,Notes,IsActive,ProductSku,SupplierSku,SupplyPrice,DeliveryDays,IsPreferred");
        foreach (var row in rows)
        {
            sb.Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Name)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.ContactPerson)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Email)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Phone)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Address)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.TaxNumber)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Notes)).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatBool(row.IsActive))).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.ProductSku)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.SupplierSku)).Append(',');
            sb.Append(row.SupplyPrice.HasValue
                ? CsvUtil.Escape(CsvUtil.FormatDecimal(row.SupplyPrice.Value))
                : string.Empty).Append(',');
            sb.Append(row.DeliveryDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.AppendLine(CsvUtil.Escape(CsvUtil.FormatBool(row.IsPreferred)));
        }

        return Result.Success(new CsvExportFile(
            "suppliers.csv",
            "text/csv",
            CsvUtil.ToUtf8Bytes(sb)));
    }

    public async Task<Result<CsvExportFile>> ExportInventoryAsync(
        int? warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (warehouseId.HasValue)
        {
            var exists = await _db.Warehouses.AnyAsync(w => w.Id == warehouseId.Value, cancellationToken);
            if (!exists)
            {
                return Result.NotFound<CsvExportFile>("Warehouse was not found.");
            }
        }

        var query = _db.Inventory.AsNoTracking().AsQueryable();
        if (warehouseId.HasValue)
        {
            query = query.Where(i => i.WarehouseId == warehouseId.Value);
        }

        var rows = await query
            .OrderBy(i => i.Warehouse.Code)
            .ThenBy(i => i.Variant.Sku)
            .Select(i => new
            {
                WarehouseCode = i.Warehouse.Code,
                WarehouseName = i.Warehouse.Name,
                i.Variant.Sku,
                ProductName = i.Variant.Product.Name,
                i.Variant.VariantName,
                i.QuantityOnHand,
                i.QuantityReserved,
                Available = i.QuantityOnHand - i.QuantityReserved,
                i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("WarehouseCode,WarehouseName,Sku,ProductName,VariantName,QuantityOnHand,QuantityReserved,AvailableQuantity,UpdatedAtUtc");
        foreach (var row in rows)
        {
            sb.Append(CsvUtil.EscapeAndSanitize(row.WarehouseCode)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.WarehouseName)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Sku)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.ProductName)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.VariantName)).Append(',');
            sb.Append(row.QuantityOnHand.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.QuantityReserved.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.Available.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(CsvUtil.Escape(row.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)));
        }

        var fileName = warehouseId.HasValue
            ? $"inventory-warehouse-{warehouseId.Value}.csv"
            : "inventory.csv";

        return Result.Success(new CsvExportFile(fileName, "text/csv", CsvUtil.ToUtf8Bytes(sb)));
    }

    public async Task<Result<CsvExportFile>> ExportOrdersAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return Result.Failure<CsvExportFile>("fromUtc must be less than or equal to toUtc.");
        }

        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toUtc.Value);
        }

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.OrderNumber,
                o.CreatedAt,
                Status = o.Status.Code,
                o.CustomerName,
                o.CustomerEmail,
                o.CustomerPhone,
                WarehouseCode = o.Warehouse == null ? null : o.Warehouse.Code,
                DeliveryMethod = o.DeliveryMethod.ToString(),
                o.Subtotal,
                o.DiscountAmount,
                o.DeliveryCost,
                o.TotalAmount,
                ItemCount = o.Items.Count
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("OrderNumber,CreatedAtUtc,Status,CustomerName,CustomerEmail,CustomerPhone,WarehouseCode,DeliveryMethod,Subtotal,DiscountAmount,DeliveryCost,TotalAmount,ItemCount");
        foreach (var row in rows)
        {
            sb.Append(CsvUtil.EscapeAndSanitize(row.OrderNumber)).Append(',');
            sb.Append(CsvUtil.Escape(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.Status)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.CustomerName)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.CustomerEmail)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.CustomerPhone)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.WarehouseCode)).Append(',');
            sb.Append(CsvUtil.EscapeAndSanitize(row.DeliveryMethod)).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.Subtotal))).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.DiscountAmount))).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.DeliveryCost))).Append(',');
            sb.Append(CsvUtil.Escape(CsvUtil.FormatDecimal(row.TotalAmount))).Append(',');
            sb.AppendLine(row.ItemCount.ToString(CultureInfo.InvariantCulture));
        }

        return Result.Success(new CsvExportFile("orders.csv", "text/csv", CsvUtil.ToUtf8Bytes(sb)));
    }
}
