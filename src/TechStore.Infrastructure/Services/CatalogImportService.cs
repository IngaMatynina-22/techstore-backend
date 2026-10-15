using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Imports;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class CatalogImportService : ICatalogImportService
{
    private static readonly string[] ExpectedHeaders =
    [
        "Sku",
        "ProductName",
        "VariantName",
        "CategorySlug",
        "BrandName",
        "PurchasePrice",
        "SellingPrice",
        "Barcode",
        "IsActive"
    ];

    private readonly ApplicationDbContext _db;

    public CatalogImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ImportBatchDto>> ImportProductsAsync(
        Stream fileStream,
        string fileName,
        long fileLength,
        CatalogImportMode mode,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.Failure<ImportBatchDto>("Authenticated user is required.");
        }

        if (fileLength <= 0)
        {
            return Result.Failure<ImportBatchDto>("CSV file is required.");
        }

        if (fileLength > CsvUtil.MaxImportBytes)
        {
            return Result.Failure<ImportBatchDto>("CSV file must be at most 2MB.");
        }

        var rawRows = await CsvUtil.ReadRowsAsync(fileStream, cancellationToken);
        if (rawRows.Count == 0)
        {
            return Result.Failure<ImportBatchDto>("CSV file is empty.");
        }

        if (rawRows.Count > CsvUtil.MaxImportRows + 1)
        {
            return Result.Failure<ImportBatchDto>("CSV file must contain at most 5000 data rows.");
        }

        var header = rawRows[0].Select(h => h.Trim()).ToArray();
        if (!HeadersMatch(header))
        {
            return Result.Failure<ImportBatchDto>(
                "Invalid CSV header. Expected: " + string.Join(",", ExpectedHeaders));
        }

        var dataRows = rawRows.Skip(1).ToList();
        var modeKey = NormalizeMode(mode);
        var now = DateTime.UtcNow;

        var batch = new ImportBatch
        {
            UserId = userId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "catalog.csv" : Path.GetFileName(fileName),
            FilePath = "inline",
            ImportType = ImportType.Products,
            Status = ImportStatus.Processing,
            Mode = modeKey,
            TotalRows = dataRows.Count,
            SuccessfulRows = 0,
            FailedRows = 0,
            CreatedAt = now
        };

        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        var parsed = new List<ParsedImportRow>(dataRows.Count);
        var skuCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < dataRows.Count; i++)
        {
            var rowNumber = i + 2;
            var cells = PadCells(dataRows[i]);
            var sanitized = cells.Select(CsvUtil.SanitizeCell).ToArray();
            var rowData = SerializeRow(sanitized);
            var row = ParseRow(rowNumber, sanitized, rowData);
            parsed.Add(row);

            if (!string.IsNullOrWhiteSpace(row.Sku))
            {
                skuCounts.TryGetValue(row.Sku, out var count);
                skuCounts[row.Sku] = count + 1;
            }
        }

        foreach (var row in parsed.Where(r => !string.IsNullOrWhiteSpace(r.Sku)))
        {
            if (skuCounts[row.Sku!] > 1)
            {
                row.AddError("Duplicate SKU within the file.");
            }
        }

        var categorySlugs = parsed
            .Where(r => r.Errors.Count == 0 && !string.IsNullOrWhiteSpace(r.CategorySlug))
            .Select(r => r.CategorySlug!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var brandNames = parsed
            .Where(r => r.Errors.Count == 0 && !string.IsNullOrWhiteSpace(r.BrandName))
            .Select(r => r.BrandName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skus = parsed
            .Where(r => r.Errors.Count == 0 && !string.IsNullOrWhiteSpace(r.Sku))
            .Select(r => r.Sku!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = categorySlugs.Count == 0
            ? []
            : await _db.Categories
                .AsNoTracking()
                .Where(c => categorySlugs.Contains(c.Slug))
                .Select(c => new { c.Id, c.Slug })
                .ToListAsync(cancellationToken);
        var categoryBySlug = categories.ToDictionary(c => c.Slug, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var brands = brandNames.Count == 0
            ? []
            : await _db.Brands
                .AsNoTracking()
                .Where(b => brandNames.Contains(b.Name))
                .Select(b => new { b.Id, b.Name })
                .ToListAsync(cancellationToken);
        var brandByName = brands.ToDictionary(b => b.Name, b => b.Id, StringComparer.OrdinalIgnoreCase);

        var existingSkuSet = skus.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.ProductVariants
                .AsNoTracking()
                .Where(v => skus.Contains(v.Sku))
                .Select(v => v.Sku)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingProductIdBySku = skus.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await _db.ProductVariants
                .AsNoTracking()
                .Where(v => skus.Contains(v.Sku))
                .ToDictionaryAsync(v => v.Sku, v => v.ProductId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in parsed)
        {
            if (row.Errors.Count > 0)
            {
                continue;
            }

            if (!categoryBySlug.TryGetValue(row.CategorySlug!, out var categoryId))
            {
                row.AddError($"Category slug '{row.CategorySlug}' was not found.");
                continue;
            }

            row.CategoryId = categoryId;

            if (!string.IsNullOrWhiteSpace(row.BrandName))
            {
                if (!brandByName.TryGetValue(row.BrandName!, out var brandId))
                {
                    row.AddError($"Brand '{row.BrandName}' was not found.");
                    continue;
                }

                row.BrandId = brandId;
            }

            var skuExists = existingSkuSet.Contains(row.Sku!);
            row.SkuExists = skuExists;
            if (existingProductIdBySku.TryGetValue(row.Sku!, out var existingProductId))
            {
                row.ExistingProductId = existingProductId;
            }

            switch (mode)
            {
                case CatalogImportMode.Create when skuExists:
                    row.AddError($"SKU '{row.Sku}' already exists.");
                    break;
                case CatalogImportMode.UpdateBySku when !skuExists:
                    row.AddError($"SKU '{row.Sku}' was not found.");
                    break;
            }
        }

        if (mode == CatalogImportMode.ValidateOnly)
        {
            await PersistRowResultsAsync(batch, parsed, writeProducts: false, userId, cancellationToken);
        }
        else
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                await PersistRowResultsAsync(batch, parsed, writeProducts: true, userId, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            });
        }

        return Result.Success((await GetBatchAsync(batch.Id, cancellationToken))!);
    }

    public async Task<ImportBatchDto?> GetBatchAsync(int batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        return batch is null ? null : Map(batch);
    }

    private async Task PersistRowResultsAsync(
        ImportBatch batch,
        List<ParsedImportRow> parsed,
        bool writeProducts,
        int userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var success = 0;
        var failed = 0;
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var existingSlugs = await _db.Products
            .AsNoTracking()
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        foreach (var slug in existingSlugs)
        {
            usedSlugs.Add(slug);
        }

        foreach (var row in parsed)
        {
            if (row.Errors.Count > 0)
            {
                failed++;
                _db.ImportRows.Add(new ImportRow
                {
                    BatchId = batch.Id,
                    RowNumber = row.RowNumber,
                    RowData = row.RowData,
                    Status = ImportRowStatus.Failed,
                    ErrorMessage = Truncate(string.Join("; ", row.Errors), 2000),
                    ProcessedAt = now
                });
                continue;
            }

            int? productId = row.ExistingProductId;

            if (writeProducts)
            {
                try
                {
                    productId = await ApplyWriteAsync(row, userId, usedSlugs, now, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    failed++;
                    _db.ImportRows.Add(new ImportRow
                    {
                        BatchId = batch.Id,
                        RowNumber = row.RowNumber,
                        RowData = row.RowData,
                        Status = ImportRowStatus.Failed,
                        ErrorMessage = Truncate(ex.Message, 2000),
                        ProcessedAt = now
                    });
                    continue;
                }
            }

            success++;
            _db.ImportRows.Add(new ImportRow
            {
                BatchId = batch.Id,
                RowNumber = row.RowNumber,
                RowData = row.RowData,
                Status = ImportRowStatus.Success,
                ProductId = productId,
                ProcessedAt = now
            });
        }

        batch.SuccessfulRows = success;
        batch.FailedRows = failed;
        batch.Status = failed == 0
            ? ImportStatus.Completed
            : success == 0
                ? ImportStatus.Failed
                : ImportStatus.CompletedWithErrors;
        batch.CompletedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ApplyWriteAsync(
        ParsedImportRow row,
        int userId,
        HashSet<string> usedSlugs,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (row.SkuExists)
        {
            var variant = await _db.ProductVariants
                .Include(v => v.Product)
                .FirstAsync(v => v.Sku == row.Sku, cancellationToken);
            var product = variant.Product;

            product.Name = row.ProductName!;
            product.CategoryId = row.CategoryId!.Value;
            product.BrandId = row.BrandId;
            product.IsActive = row.IsActive;
            product.UpdatedBy = userId;
            product.UpdatedAt = now;

            variant.VariantName = string.IsNullOrWhiteSpace(row.VariantName) ? "Default" : row.VariantName!;
            variant.PurchasePrice = row.PurchasePrice;
            variant.SellingPrice = row.SellingPrice;
            variant.Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode;
            variant.IsActive = row.IsActive;
            variant.UpdatedAt = now;

            return product.Id;
        }

        var slug = await AllocateSlugAsync(row.ProductName!, usedSlugs, cancellationToken);
        var productEntity = new Product
        {
            Name = row.ProductName!,
            Slug = slug,
            CategoryId = row.CategoryId!.Value,
            BrandId = row.BrandId,
            IsActive = row.IsActive,
            CreatedBy = userId,
            CreatedAt = now,
            Variants =
            {
                new ProductVariant
                {
                    Sku = row.Sku!,
                    Barcode = string.IsNullOrWhiteSpace(row.Barcode) ? null : row.Barcode,
                    VariantName = string.IsNullOrWhiteSpace(row.VariantName) ? "Default" : row.VariantName!,
                    PurchasePrice = row.PurchasePrice,
                    SellingPrice = row.SellingPrice,
                    IsActive = row.IsActive,
                    CreatedAt = now
                }
            }
        };

        _db.Products.Add(productEntity);
        await _db.SaveChangesAsync(cancellationToken);
        usedSlugs.Add(slug);
        return productEntity.Id;
    }

    private static async Task<string> AllocateSlugAsync(
        string productName,
        HashSet<string> usedSlugs,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var baseSlug = SlugHelper.Generate(productName);
        if (string.IsNullOrWhiteSpace(baseSlug) || !SlugHelper.IsValid(baseSlug))
        {
            baseSlug = "product";
        }

        if (!usedSlugs.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!usedSlugs.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(100, baseSlug.Length + 33)];
    }

    private static ParsedImportRow ParseRow(int rowNumber, string[] cells, string rowData)
    {
        var row = new ParsedImportRow
        {
            RowNumber = rowNumber,
            RowData = rowData,
            Sku = NullIfEmpty(cells[0]),
            ProductName = NullIfEmpty(cells[1]),
            VariantName = NullIfEmpty(cells[2]),
            CategorySlug = NullIfEmpty(cells[3]),
            BrandName = NullIfEmpty(cells[4]),
            Barcode = NullIfEmpty(cells[7])
        };

        if (string.IsNullOrWhiteSpace(row.Sku))
        {
            row.AddError("SKU is required.");
        }
        else if (row.Sku.Length > 100)
        {
            row.AddError("SKU must be at most 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(row.ProductName))
        {
            row.AddError("ProductName is required.");
        }
        else if (row.ProductName.Length > 300)
        {
            row.AddError("ProductName must be at most 300 characters.");
        }

        if (string.IsNullOrWhiteSpace(row.CategorySlug))
        {
            row.AddError("CategorySlug is required.");
        }

        if (!TryParseDecimal(cells[5], out var purchasePrice))
        {
            row.AddError("PurchasePrice is invalid.");
        }
        else if (purchasePrice < 0)
        {
            row.AddError("PurchasePrice cannot be negative.");
        }
        else
        {
            row.PurchasePrice = purchasePrice;
        }

        if (!TryParseDecimal(cells[6], out var sellingPrice))
        {
            row.AddError("SellingPrice is invalid.");
        }
        else if (sellingPrice < 0)
        {
            row.AddError("SellingPrice cannot be negative.");
        }
        else
        {
            row.SellingPrice = sellingPrice;
        }

        if (!string.IsNullOrWhiteSpace(row.Barcode) && row.Barcode.Length > 100)
        {
            row.AddError("Barcode must be at most 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(cells[8]))
        {
            row.IsActive = true;
        }
        else if (!TryParseBool(cells[8], out var isActive))
        {
            row.AddError("IsActive is invalid.");
        }
        else
        {
            row.IsActive = isActive;
        }

        return row;
    }

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        var text = (raw ?? string.Empty).Trim().TrimStart('\'');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseBool(string? raw, out bool value)
    {
        var text = (raw ?? string.Empty).Trim().TrimStart('\'').ToLowerInvariant();
        switch (text)
        {
            case "true":
            case "1":
            case "yes":
            case "y":
                value = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "n":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static string[] PadCells(string[] cells)
    {
        if (cells.Length >= ExpectedHeaders.Length)
        {
            return cells.Take(ExpectedHeaders.Length).ToArray();
        }

        var padded = new string[ExpectedHeaders.Length];
        Array.Copy(cells, padded, cells.Length);
        for (var i = cells.Length; i < padded.Length; i++)
        {
            padded[i] = string.Empty;
        }

        return padded;
    }

    private static bool HeadersMatch(string[] header)
    {
        if (header.Length < ExpectedHeaders.Length)
        {
            return false;
        }

        for (var i = 0; i < ExpectedHeaders.Length; i++)
        {
            if (!string.Equals(header[i], ExpectedHeaders[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeMode(CatalogImportMode mode) => mode switch
    {
        CatalogImportMode.ValidateOnly => "validate",
        CatalogImportMode.Create => "create",
        CatalogImportMode.UpdateBySku => "updatebysku",
        CatalogImportMode.UpsertBySku => "upsertbysku",
        _ => "validate"
    };

    private static string SerializeRow(string[] cells) =>
        JsonSerializer.Serialize(ExpectedHeaders
            .Select((h, i) => new KeyValuePair<string, string>(h, i < cells.Length ? cells[i] : string.Empty))
            .ToDictionary(kv => kv.Key, kv => kv.Value));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimStart('\'');

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static ImportBatchDto Map(ImportBatch batch) =>
        new(
            batch.Id,
            batch.FileName,
            batch.Mode,
            batch.ImportType.ToString(),
            batch.Status.ToString(),
            batch.TotalRows,
            batch.SuccessfulRows,
            batch.FailedRows,
            batch.CreatedAt,
            batch.CompletedAt,
            batch.Rows
                .OrderBy(r => r.RowNumber)
                .Select(r => new ImportRowResultDto(
                    r.RowNumber,
                    r.Status.ToString(),
                    r.ErrorMessage,
                    r.ProductId,
                    r.RowData))
                .ToList());

    private sealed class ParsedImportRow
    {
        public int RowNumber { get; init; }
        public string RowData { get; init; } = string.Empty;
        public string? Sku { get; set; }
        public string? ProductName { get; set; }
        public string? VariantName { get; set; }
        public string? CategorySlug { get; set; }
        public string? BrandName { get; set; }
        public string? Barcode { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsActive { get; set; } = true;
        public int? CategoryId { get; set; }
        public int? BrandId { get; set; }
        public bool SkuExists { get; set; }
        public int? ExistingProductId { get; set; }
        public List<string> Errors { get; } = [];

        public void AddError(string error) => Errors.Add(error);
    }
}
