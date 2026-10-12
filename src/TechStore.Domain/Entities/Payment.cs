using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class Payment : AuditableEntity
{
    public int OrderId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Comment { get; set; }

    public Order Order { get; set; } = null!;
}
