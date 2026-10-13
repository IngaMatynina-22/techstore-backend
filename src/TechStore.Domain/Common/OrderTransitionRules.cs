namespace TechStore.Domain.Common;

public static class OrderTransitionRules
{
    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.Ordinal)
    {
        [OrderStatusCodes.New] = [OrderStatusCodes.Confirmed, OrderStatusCodes.Cancelled],
        [OrderStatusCodes.Confirmed] = [OrderStatusCodes.Processing, OrderStatusCodes.Cancelled],
        [OrderStatusCodes.Processing] =
        [
            OrderStatusCodes.ReadyForShipment,
            OrderStatusCodes.Completed,
            OrderStatusCodes.Cancelled
        ],
        [OrderStatusCodes.ReadyForShipment] =
        [
            OrderStatusCodes.Shipped,
            OrderStatusCodes.Completed,
            OrderStatusCodes.Cancelled
        ],
        [OrderStatusCodes.Shipped] = [OrderStatusCodes.Delivered],
        [OrderStatusCodes.Delivered] = [OrderStatusCodes.Completed]
    };

    private static readonly HashSet<string> Cancellable = new(StringComparer.Ordinal)
    {
        OrderStatusCodes.New,
        OrderStatusCodes.Confirmed,
        OrderStatusCodes.Processing,
        OrderStatusCodes.ReadyForShipment
    };

    private static readonly HashSet<string> Fulfillable = new(StringComparer.Ordinal)
    {
        OrderStatusCodes.Processing,
        OrderStatusCodes.ReadyForShipment
    };

    public static bool CanTransition(string fromCode, string toCode)
        => Allowed.TryGetValue(fromCode, out var next) && next.Contains(toCode);

    public static bool CanCancel(string statusCode) => Cancellable.Contains(statusCode);

    public static bool CanFulfill(string statusCode) => Fulfillable.Contains(statusCode);

    public static bool IsFinal(string statusCode) =>
        statusCode is OrderStatusCodes.Delivered
            or OrderStatusCodes.Completed
            or OrderStatusCodes.Cancelled
            or OrderStatusCodes.Returned;
}
