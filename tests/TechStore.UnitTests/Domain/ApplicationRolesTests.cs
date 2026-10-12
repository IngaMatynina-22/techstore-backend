using TechStore.Domain.Common;

namespace TechStore.UnitTests.Domain;

public class ApplicationRolesTests
{
    [Fact]
    public void All_ContainsExpectedRoles()
    {
        Assert.Contains(ApplicationRoles.Admin, ApplicationRoles.All);
        Assert.Contains(ApplicationRoles.Manager, ApplicationRoles.All);
        Assert.Contains(ApplicationRoles.Customer, ApplicationRoles.All);
        Assert.Equal(3, ApplicationRoles.All.Count);
    }
}
