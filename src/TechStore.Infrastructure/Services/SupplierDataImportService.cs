using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Suppliers;
using TechStore.Domain.Entities;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class SupplierDataImportService : ISupplierDataImportService
{
    private static readonly string[] ExpectedHeaders =
    [
        "SupplierId",
        "SupplierName",
        "ContactPerson",
        "Email",
        "Phone",
        "Address",
        "TaxNumber",
        "Notes",
        "IsActive",
        "ProductSku",
        "SupplierSku",
        "SupplyPrice",
        "DeliveryDays",
        "IsPreferred"
    ];

    private readonly ApplicationDbContext _db;

    public SupplierDataImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SupplierDataImportResult>> ImportAsync(
        Stream fileStream,
        string fileName,
        long fileLength,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result.Failure<SupplierDataImportResult>("Authenticated user is required.");
        }

        if (fileLength <= 0 || fileLength > CsvUtil.MaxImportBytes)
        {
            return Result.Failure<SupplierDataImportResult>("CSV file must be between 1 byte and 2MB.");
        }

        List<string[]> rows;
        try
        {
            rows = await CsvUtil.ReadRowsAsync(fileStream, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Result.Failure<SupplierDataImportResult>(exception.Message);
        }
        if (rows.Count == 0)
        {
            return Result.Failure<SupplierDataImportResult>("CSV file is empty.");
        }

        if (rows.Count > CsvUtil.MaxImportRows + 1)
        {
            return Result.Failure<SupplierDataImportResult>("CSV file must contain at most 5000 data rows.");
        }

        if (!rows[0].Select(value => value.Trim()).SequenceEqual(ExpectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<SupplierDataImportResult>(
                "Invalid CSV header. Expected: " + string.Join(",", ExpectedHeaders));
        }

        var dataRows = rows.Skip(1).ToList();
        var parsed = new List<ParsedRow>(dataRows.Count);
        for (var index = 0; index < dataRows.Count; index++)
        {
            var cells = dataRows[index];
            var values = Enumerable.Range(0, ExpectedHeaders.Length)
                .Select(column => column < cells.Length ? cells[column].Trim() : string.Empty)
                .ToArray();
            parsed.Add(ParseRow(index + 2, values));
        }

        var errors = parsed.Where(row => row.Error is not null).ToList();
        var supplierGroups = parsed
            .Where(row => row.Error is null)
            .GroupBy(row => !string.IsNullOrWhiteSpace(row.TaxNumber)
                ? $"tax:{row.TaxNumber.Trim().ToUpperInvariant()}"
                : $"name:{row.SupplierName.Trim().ToUpperInvariant()}")
            .ToList();

        foreach (var group in supplierGroups)
        {
            var first = group.First();
            if (group.Any(row => !SameSupplierData(first, row)))
            {
                foreach (var row in group)
                {
                    row.Error = "Supplier details must be identical on every row for that supplier.";
                    errors.Add(row);
                }
            }

            var duplicateSkus = group
                .Where(row => row.ProductSku is not null)
                .GroupBy(row => row.ProductSku!, StringComparer.OrdinalIgnoreCase)
                .Where(skus => skus.Count() > 1);
            foreach (var duplicate in duplicateSkus)
            {
                foreach (var row in duplicate)
                {
                    row.Error = $"Product SKU '{row.ProductSku}' appears more than once for this supplier.";
                    errors.Add(row);
                }
            }
        }

        if (errors.Count == 0)
        {
            await ValidateReferencesAsync(supplierGroups, errors, cancellationToken);
        }

        if (errors.Count > 0)
        {
            var message = string.Join(" ", errors
                .Distinct()
                .Select(row => $"Row {row.RowNumber}: {row.Error}"));
            return Result.Failure<SupplierDataImportResult>(message);
        }

        var existingSuppliers = await _db.Suppliers.ToListAsync(cancellationToken);
        var suppliersById = existingSuppliers.ToDictionary(supplier => supplier.Id);
        var suppliersByName = existingSuppliers
            .GroupBy(supplier => supplier.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var productSkus = parsed
            .Where(row => row.ProductSku is not null)
            .Select(row => row.ProductSku!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var variantsBySku = await _db.ProductVariants
            .Include(variant => variant.Product)
            .Where(variant => productSkus.Contains(variant.Sku))
            .ToDictionaryAsync(variant => variant.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var createdSuppliers = 0;
        var updatedSuppliers = 0;
        var upsertedProducts = 0;
        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            foreach (var group in supplierGroups)
            {
                var first = group.First();
                Supplier? supplier = null;
                if (first.SupplierId.HasValue)
                {
                    suppliersById.TryGetValue(first.SupplierId.Value, out supplier);
                }

                if (supplier is null && !string.IsNullOrWhiteSpace(first.TaxNumber))
                {
                    supplier = existingSuppliers.FirstOrDefault(item =>
                        string.Equals(item.TaxNumber, first.TaxNumber, StringComparison.OrdinalIgnoreCase));
                }

                if (supplier is null)
                {
                    suppliersByName.TryGetValue(first.SupplierName, out supplier);
                }

                if (supplier is null)
                {
                    supplier = new Supplier { CreatedAt = now };
                    _db.Suppliers.Add(supplier);
                    createdSuppliers++;
                }
                else
                {
                    updatedSuppliers++;
                    suppliersByName.Remove(supplier.Name);
                }

                supplier.Name = first.SupplierName;
                supplier.ContactPerson = Normalize(first.ContactPerson);
                supplier.Email = Normalize(first.Email);
                supplier.Phone = Normalize(first.Phone);
                supplier.Address = Normalize(first.Address);
                supplier.TaxNumber = Normalize(first.TaxNumber);
                supplier.Notes = Normalize(first.Notes);
                supplier.IsActive = first.IsActive;
                supplier.UpdatedAt = supplier.Id == 0 ? null : now;
                suppliersByName[supplier.Name] = supplier;

                foreach (var row in group.Where(row => row.ProductSku is not null))
                {
                    var variant = variantsBySku[row.ProductSku!];
                    var link = await _db.SupplierProducts.FirstOrDefaultAsync(
                        item => item.SupplierId == supplier.Id && item.VariantId == variant.Id,
                        cancellationToken);
                    if (link is null)
                    {
                        link = new SupplierProduct
                        {
                            Supplier = supplier,
                            VariantId = variant.Id
                        };
                        _db.SupplierProducts.Add(link);
                    }

                    link.SupplierSku = Normalize(row.SupplierSku);
                    link.SupplyPrice = row.SupplyPrice!.Value;
                    link.DeliveryDays = row.DeliveryDays;
                    link.IsPreferred = row.IsPreferred;
                    upsertedProducts++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            foreach (var group in supplierGroups)
            {
                var supplierName = group.First().SupplierName;
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    EntityType = "Supplier",
                    EntityId = group.First().SupplierId?.ToString(CultureInfo.InvariantCulture) ?? supplierName,
                    Action = AuditAction.Update,
                    NewValues = $"Imported supplier '{supplierName}'",
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var successfulRows = parsed
            .Select(row => new SupplierDataImportRowResult(row.RowNumber, "Success", null))
            .ToList();
        return Result.Success(new SupplierDataImportResult(
            Path.GetFileName(fileName),
            dataRows.Count,
            createdSuppliers,
            updatedSuppliers,
            upsertedProducts,
            successfulRows));
    }

    private async Task ValidateReferencesAsync(
        IEnumerable<IGrouping<string, ParsedRow>> supplierGroups,
        List<ParsedRow> errors,
        CancellationToken cancellationToken)
    {
        var rows = supplierGroups.SelectMany(group => group).ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        var nameOwners = suppliers
            .GroupBy(supplier => supplier.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var finalNames = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in supplierGroups)
        {
            var row = group.First();
            var supplier = row.SupplierId.HasValue
                ? suppliers.FirstOrDefault(item => item.Id == row.SupplierId.Value)
                : null;
            supplier ??= !string.IsNullOrWhiteSpace(row.TaxNumber)
                ? suppliers.FirstOrDefault(item => string.Equals(
                    item.TaxNumber,
                    row.TaxNumber,
                    StringComparison.OrdinalIgnoreCase))
                : null;
            supplier ??= suppliers.FirstOrDefault(item => string.Equals(
                item.Name,
                row.SupplierName,
                StringComparison.OrdinalIgnoreCase));

            if (row.SupplierId.HasValue &&
                suppliers.FirstOrDefault(item => item.Id == row.SupplierId.Value) is { } idOwner &&
                !string.Equals(idOwner.Name, row.SupplierName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(row.TaxNumber) ||
                 !string.Equals(idOwner.TaxNumber, row.TaxNumber, StringComparison.OrdinalIgnoreCase)))
            {
                row.Error = $"Supplier ID '{row.SupplierId.Value}' belongs to a different supplier.";
                errors.Add(row);
            }

            if (nameOwners.TryGetValue(row.SupplierName, out var ownerId) &&
                (supplier is null || ownerId != supplier.Id))
            {
                row.Error = $"Supplier name '{row.SupplierName}' already belongs to another supplier.";
                errors.Add(row);
            }

            if (finalNames.TryGetValue(row.SupplierName, out var previousId) &&
                (supplier is null || previousId != supplier.Id))
            {
                row.Error = $"Supplier name '{row.SupplierName}' is duplicated in the file.";
                errors.Add(row);
            }

            finalNames[row.SupplierName] = supplier?.Id;
        }

        var requestedSkus = rows
            .Where(row => row.ProductSku is not null)
            .Select(row => row.ProductSku!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var foundSkus = requestedSkus.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.ProductVariants.AsNoTracking()
                .Where(variant => requestedSkus.Contains(variant.Sku))
                .Select(variant => variant.Sku)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(row => row.ProductSku is not null && !foundSkus.Contains(row.ProductSku)))
        {
            row.Error = $"Product SKU '{row.ProductSku}' was not found.";
            errors.Add(row);
        }
    }

    private static ParsedRow ParseRow(int rowNumber, string[] values)
    {
        var row = new ParsedRow(rowNumber)
        {
            SupplierName = values[1],
            ContactPerson = values[2],
            Email = values[3],
            Phone = values[4],
            Address = values[5],
            TaxNumber = values[6],
            Notes = values[7],
            ProductSku = EmptyToNull(values[9]),
            SupplierSku = values[10],
            IsPreferred = false
        };

        if (values[0].Length > 0)
        {
            if (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var supplierId) ||
                supplierId <= 0)
            {
                row.Error = "SupplierId must be a positive integer or blank.";
                return row;
            }

            row.SupplierId = supplierId;
        }

        if (string.IsNullOrWhiteSpace(row.SupplierName) || row.SupplierName.Length > 200)
        {
            row.Error = "SupplierName is required and must be at most 200 characters.";
            return row;
        }

        if (row.ContactPerson.Length > 200 ||
            row.Email.Length > 256 ||
            row.Phone.Length > 50 ||
            row.Address.Length > 500 ||
            row.TaxNumber.Length > 50 ||
            row.Notes.Length > 2000 ||
            row.ProductSku?.Length > 100 ||
            row.SupplierSku.Length > 100)
        {
            row.Error = "One or more supplier or product fields exceed their maximum length.";
            return row;
        }

        if (!TryBool(values[8], true, out var isActive))
        {
            row.Error = "IsActive must be true or false.";
            return row;
        }

        row.IsActive = isActive;
        if (row.ProductSku is null)
        {
            if (values.Skip(10).Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                row.Error = "Supplier product fields require ProductSku.";
            }

            return row;
        }

        if (!decimal.TryParse(values[11], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
        {
            row.Error = "SupplyPrice must be a non-negative number when ProductSku is provided.";
            return row;
        }

        row.SupplyPrice = price;
        if (values[12].Length > 0)
        {
            if (!int.TryParse(values[12], NumberStyles.None, CultureInfo.InvariantCulture, out var deliveryDays) ||
                deliveryDays < 0)
            {
                row.Error = "DeliveryDays must be a non-negative integer or blank.";
                return row;
            }

            row.DeliveryDays = deliveryDays;
        }

        if (!TryBool(values[13], false, out var isPreferred))
        {
            row.Error = "IsPreferred must be true or false.";
            return row;
        }

        row.IsPreferred = isPreferred;
        return row;
    }

    private static bool SameSupplierData(ParsedRow left, ParsedRow right) =>
        left.SupplierId == right.SupplierId &&
        string.Equals(left.SupplierName, right.SupplierName, StringComparison.OrdinalIgnoreCase) &&
        left.ContactPerson == right.ContactPerson &&
        left.Email == right.Email &&
        left.Phone == right.Phone &&
        left.Address == right.Address &&
        left.TaxNumber == right.TaxNumber &&
        left.Notes == right.Notes &&
        left.IsActive == right.IsActive;

    private static bool TryBool(string value, bool defaultValue, out bool result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = defaultValue;
            return true;
        }

        return bool.TryParse(value, out result);
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Normalize(string value) => EmptyToNull(value);

    private sealed class ParsedRow(int rowNumber)
    {
        public int RowNumber { get; } = rowNumber;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? ProductSku { get; set; }
        public string SupplierSku { get; set; } = string.Empty;
        public decimal? SupplyPrice { get; set; }
        public int? DeliveryDays { get; set; }
        public bool IsPreferred { get; set; }
        public string? Error { get; set; }
    }
}
