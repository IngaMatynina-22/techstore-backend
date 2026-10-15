using TechStore.Application.Common;

namespace TechStore.Application.Features.Exports;

public record CsvExportFile(string FileName, string ContentType, byte[] Content);

public interface ICatalogExportService
{
    Task<Result<CsvExportFile>> ExportProductsAsync(CancellationToken cancellationToken = default);

    Task<Result<CsvExportFile>> ExportInventoryAsync(
        int? warehouseId,
        CancellationToken cancellationToken = default);

    Task<Result<CsvExportFile>> ExportOrdersAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}
