namespace TechStore.Application.Features.Imports;

public enum CatalogImportMode
{
    ValidateOnly = 0,
    Create = 1,
    UpdateBySku = 2,
    UpsertBySku = 3
}

public record ImportRowResultDto(
    int RowNumber,
    string Status,
    string? ErrorMessage,
    int? ProductId,
    string RowData);

public record ImportBatchDto(
    int Id,
    string FileName,
    string Mode,
    string ImportType,
    string Status,
    int TotalRows,
    int SuccessfulRows,
    int FailedRows,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ImportRowResultDto> Rows);
