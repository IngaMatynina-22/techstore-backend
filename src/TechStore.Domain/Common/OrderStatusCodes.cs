namespace TechStore.Domain.Common;

/// <summary>Matches seeded OrderStatuses.Code values.</summary>
public static class OrderStatusCodes
{
    public const string New = "New";
    public const string Confirmed = "Confirmed";
    public const string Processing = "Processing";
    public const string ReadyForShipment = "ReadyForShipment";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Returned = "Returned";
}
