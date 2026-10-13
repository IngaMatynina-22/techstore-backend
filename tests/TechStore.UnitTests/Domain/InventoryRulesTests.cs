using TechStore.Domain.Common;

namespace TechStore.UnitTests.Domain;

public class InventoryRulesTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public void IsPositiveQuantity(int quantity, bool expected)
        => Assert.Equal(expected, InventoryRules.IsPositiveQuantity(quantity));

    [Fact]
    public void CanIssue_WhenAvailableInsufficient_ReturnsFalse()
        => Assert.False(InventoryRules.CanIssue(quantityOnHand: 10, quantityReserved: 4, quantityToIssue: 7));

    [Fact]
    public void CanIssue_WhenAvailableSufficient_ReturnsTrue()
        => Assert.True(InventoryRules.CanIssue(quantityOnHand: 10, quantityReserved: 2, quantityToIssue: 7));

    [Fact]
    public void ComputeAdjustmentDelta_ReturnsSignedDifference()
        => Assert.Equal(-3, InventoryRules.ComputeAdjustmentDelta(12, 9));

    [Fact]
    public void IsBelowThreshold_WhenConfiguredAndReached_ReturnsTrue()
        => Assert.True(InventoryRules.IsBelowThreshold(3, 5));

    [Fact]
    public void IsBelowThreshold_WhenNotConfigured_ReturnsFalse()
        => Assert.False(InventoryRules.IsBelowThreshold(0, null));
}
