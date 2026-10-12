namespace TechStore.Domain.Common;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        Manager,
        Customer
    ];
}
