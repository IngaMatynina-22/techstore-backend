using TechStore.Application.Common;

namespace TechStore.Application.Features.Imports;

public interface ICatalogImportService
{
    Task<Result<ImportBatchDto>> ImportProductsAsync(
        Stream fileStream,
        string fileName,
        long fileLength,
        CatalogImportMode mode,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ImportBatchDto?> GetBatchAsync(int batchId, CancellationToken cancellationToken = default);
}
