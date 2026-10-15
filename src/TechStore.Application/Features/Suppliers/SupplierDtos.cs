namespace TechStore.Application.Features.Suppliers;

public class SupplierQuery
{
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpsertSupplierProductRequest
{
    public int VariantId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal SupplyPrice { get; set; }
    public int? DeliveryDays { get; set; }
    public bool IsPreferred { get; set; }
}

public record SupplierDto(
    int Id,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record SupplierProductDto(
    int Id,
    int SupplierId,
    int VariantId,
    string Sku,
    string VariantName,
    string ProductName,
    string? SupplierSku,
    decimal SupplyPrice,
    int? DeliveryDays,
    bool IsPreferred);
