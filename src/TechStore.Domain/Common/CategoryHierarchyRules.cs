namespace TechStore.Domain.Common;

public static class CategoryHierarchyRules
{
    /// <summary>
    /// Returns true when assigning <paramref name="newParentId"/> to <paramref name="categoryId"/>
    /// would introduce a cycle in the parent chain.
    /// </summary>
    public static bool WouldCreateCycle(int categoryId, int? newParentId, Func<int, int?> getParentId)
    {
        if (newParentId is null)
        {
            return false;
        }

        if (newParentId.Value == categoryId)
        {
            return true;
        }

        var current = newParentId;
        var visited = new HashSet<int>();

        while (current.HasValue)
        {
            if (current.Value == categoryId)
            {
                return true;
            }

            if (!visited.Add(current.Value))
            {
                return true;
            }

            current = getParentId(current.Value);
        }

        return false;
    }
}
