using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

public class Customer : AuditableEntity
{
    /// <summary>
    /// Optional link to ASP.NET Identity user (Customer role). Unique when set.
    /// </summary>
    public int? UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CustomerAddress> Addresses { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
