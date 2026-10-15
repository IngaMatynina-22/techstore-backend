using TechStore.Domain.Enums;

namespace TechStore.Domain.Common;

public static class ReturnTransitionRules
{
    private static readonly Dictionary<ReturnRequestStatus, HashSet<ReturnRequestStatus>> Allowed = new()
    {
        [ReturnRequestStatus.Requested] = [ReturnRequestStatus.Approved, ReturnRequestStatus.Rejected],
        [ReturnRequestStatus.Approved] = [ReturnRequestStatus.Received, ReturnRequestStatus.Rejected],
        [ReturnRequestStatus.Received] = [ReturnRequestStatus.Restocked, ReturnRequestStatus.Closed],
        [ReturnRequestStatus.Restocked] = [ReturnRequestStatus.Closed]
    };

    public static bool CanTransition(ReturnRequestStatus from, ReturnRequestStatus to)
        => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static bool IsTerminal(ReturnRequestStatus status)
        => status is ReturnRequestStatus.Rejected or ReturnRequestStatus.Closed;
}
