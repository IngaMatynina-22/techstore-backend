using TechStore.Domain.Enums;

namespace TechStore.Domain.Common;

public static class PurchaseOrderTransitionRules
{
    private static readonly Dictionary<PurchaseOrderStatus, HashSet<PurchaseOrderStatus>> Allowed = new()
    {
        [PurchaseOrderStatus.Draft] = [PurchaseOrderStatus.Confirmed, PurchaseOrderStatus.Cancelled],
        [PurchaseOrderStatus.Confirmed] =
        [
            PurchaseOrderStatus.PartiallyReceived,
            PurchaseOrderStatus.Received,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.PartiallyReceived] =
        [
            PurchaseOrderStatus.PartiallyReceived,
            PurchaseOrderStatus.Received
        ]
    };

    public static bool CanTransition(PurchaseOrderStatus from, PurchaseOrderStatus to)
        => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static bool CanEditItems(PurchaseOrderStatus status) => status == PurchaseOrderStatus.Draft;

    public static bool CanReceive(PurchaseOrderStatus status)
        => status is PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived;

    public static bool CanCancel(PurchaseOrderStatus status)
        => status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Confirmed;
}
