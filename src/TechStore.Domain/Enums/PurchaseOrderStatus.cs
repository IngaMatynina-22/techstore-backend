namespace TechStore.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4
}
