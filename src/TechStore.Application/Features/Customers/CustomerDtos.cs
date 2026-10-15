using TechStore.Application.Common;

namespace TechStore.Application.Features.Customers;

public class CustomerListItemDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerQuery
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public bool IncludeInactive { get; set; } = true;
}

public static class CustomerQueryExtensions
{
    public static PagedResult<CustomerListItemDto> EmptyPage(int page, int pageSize) =>
        new()
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
        };
}
