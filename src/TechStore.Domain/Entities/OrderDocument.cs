using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class OrderDocument : BaseEntity
{
    public int OrderId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
}
