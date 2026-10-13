namespace TechStore.Domain.Common;

public static class InventoryRules
{
    public static bool IsPositiveQuantity(int quantity) => quantity > 0;

    public static bool CanIssue(int quantityOnHand, int quantityReserved, int quantityToIssue)
    {
        if (quantityToIssue <= 0)
        {
            return false;
        }

        return quantityOnHand - quantityReserved >= quantityToIssue;
    }

    public static int Available(int quantityOnHand, int quantityReserved)
        => quantityOnHand - quantityReserved;

    public static bool IsBelowThreshold(int quantityOnHand, int? threshold)
        => threshold.HasValue && quantityOnHand <= threshold.Value;

    public static int ComputeAdjustmentDelta(int currentOnHand, int targetOnHand)
        => targetOnHand - currentOnHand;
}
