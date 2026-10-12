namespace TechStore.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Failed = 4,
    Refunded = 5,
    Cancelled = 6
}
