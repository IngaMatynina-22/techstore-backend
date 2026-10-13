using TechStore.Domain.Common;

namespace TechStore.UnitTests.Domain;

public class OrderTransitionRulesTests
{
    [Theory]
    [InlineData(OrderStatusCodes.New, OrderStatusCodes.Confirmed, true)]
    [InlineData(OrderStatusCodes.New, OrderStatusCodes.Cancelled, true)]
    [InlineData(OrderStatusCodes.New, OrderStatusCodes.Completed, false)]
    [InlineData(OrderStatusCodes.Confirmed, OrderStatusCodes.Processing, true)]
    [InlineData(OrderStatusCodes.Processing, OrderStatusCodes.Completed, true)]
    [InlineData(OrderStatusCodes.Completed, OrderStatusCodes.Cancelled, false)]
    [InlineData(OrderStatusCodes.Shipped, OrderStatusCodes.Cancelled, false)]
    public void CanTransition_MatchesTable(string from, string to, bool expected)
        => Assert.Equal(expected, OrderTransitionRules.CanTransition(from, to));

    [Fact]
    public void CanCancel_AllowsPreFulfillmentStatuses()
    {
        Assert.True(OrderTransitionRules.CanCancel(OrderStatusCodes.New));
        Assert.True(OrderTransitionRules.CanCancel(OrderStatusCodes.Processing));
        Assert.False(OrderTransitionRules.CanCancel(OrderStatusCodes.Completed));
        Assert.False(OrderTransitionRules.CanCancel(OrderStatusCodes.Shipped));
    }

    [Fact]
    public void CanFulfill_OnlyProcessingOrReady()
    {
        Assert.True(OrderTransitionRules.CanFulfill(OrderStatusCodes.Processing));
        Assert.True(OrderTransitionRules.CanFulfill(OrderStatusCodes.ReadyForShipment));
        Assert.False(OrderTransitionRules.CanFulfill(OrderStatusCodes.New));
        Assert.False(OrderTransitionRules.CanFulfill(OrderStatusCodes.Confirmed));
    }
}
