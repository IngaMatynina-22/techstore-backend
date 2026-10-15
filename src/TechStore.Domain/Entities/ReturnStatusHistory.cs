using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class ReturnStatusHistory : BaseEntity
{
    public int ReturnRequestId { get; set; }
    public ReturnRequestStatus? OldStatus { get; set; }
    public ReturnRequestStatus NewStatus { get; set; }
    public int? ChangedBy { get; set; }
    public string? Comment { get; set; }
    public DateTime ChangedAt { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
}
