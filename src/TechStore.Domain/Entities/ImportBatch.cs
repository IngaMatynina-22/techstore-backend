using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class ImportBatch : BaseEntity
{
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ImportType ImportType { get; set; }
    public ImportStatus Status { get; set; }

    /// <summary>Import write mode, e.g. validate, create, updatebysku, upsertbysku.</summary>
    public string Mode { get; set; } = "validate";

    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<ImportRow> Rows { get; set; } = [];
}
