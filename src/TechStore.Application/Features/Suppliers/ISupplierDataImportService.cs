using TechStore.Application.Common;

namespace TechStore.Application.Features.Suppliers;

public interface ISupplierDataImportService
{
    Task<Result<SupplierDataImportResult>> ImportAsync(
        Stream fileStream,
        string fileName,
        long fileLength,
        int userId,
        CancellationToken cancellationToken = default);
}

public record SupplierDataImportRowResult(int RowNumber, string Status, string? ErrorMessage);

public record SupplierDataImportResult(
    string FileName,
    int TotalRows,
    int CreatedSuppliers,
    int UpdatedSuppliers,
    int UpsertedProducts,
    IReadOnlyList<SupplierDataImportRowResult> Rows);
