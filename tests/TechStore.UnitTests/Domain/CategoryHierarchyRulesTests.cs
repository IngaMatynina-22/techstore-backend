using TechStore.Domain.Common;

namespace TechStore.UnitTests.Domain;

public class CategoryHierarchyRulesTests
{
    [Fact]
    public void WouldCreateCycle_WhenParentIsSelf_ReturnsTrue()
    {
        var result = CategoryHierarchyRules.WouldCreateCycle(5, 5, _ => null);
        Assert.True(result);
    }

    [Fact]
    public void WouldCreateCycle_WhenAncestorChainLoopsBack_ReturnsTrue()
    {
        // 1 -> 2 -> 3; assigning parent of 1 to 3 would cycle if we set 3's parent to 1
        var parents = new Dictionary<int, int?>
        {
            [1] = null,
            [2] = 1,
            [3] = 2
        };

        var result = CategoryHierarchyRules.WouldCreateCycle(
            1,
            3,
            id => parents.TryGetValue(id, out var parent) ? parent : null);

        Assert.True(result);
    }

    [Fact]
    public void WouldCreateCycle_WhenParentIsValid_ReturnsFalse()
    {
        var parents = new Dictionary<int, int?>
        {
            [1] = null,
            [2] = 1
        };

        var result = CategoryHierarchyRules.WouldCreateCycle(
            3,
            2,
            id => parents.TryGetValue(id, out var parent) ? parent : null);

        Assert.False(result);
    }

    [Fact]
    public void WouldCreateCycle_WhenParentIsNull_ReturnsFalse()
    {
        Assert.False(CategoryHierarchyRules.WouldCreateCycle(1, null, _ => null));
    }
}
