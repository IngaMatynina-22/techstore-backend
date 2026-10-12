using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class ImportRow : BaseEntity
{
    public int BatchId { get; set; }
    public int RowNumber { get; set; }
    public string RowData { get; set; } = string.Empty;
    public ImportRowStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ProductId { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public ImportBatch Batch { get; set; } = null!;
    public Product? Product { get; set; }
}
