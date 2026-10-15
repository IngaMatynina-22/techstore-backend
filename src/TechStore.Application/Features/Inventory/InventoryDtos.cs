using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Inventory;

public record WarehouseDto(
    int Id,
    string Name,
    string Code,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class CreateWarehouseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class InventoryQuery
{
    public int? WarehouseId { get; set; }
    public int? VariantId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public int? MaxQuantityOnHand { get; set; }
    public bool? OutOfStockOnly { get; set; }
    public bool? LowStockOnly { get; set; }
    public string SortBy { get; set; } = "sku";
    public bool SortDesc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record InventoryItemDto(
    int Id,
    int WarehouseId,
    string WarehouseName,
    string WarehouseCode,
    int VariantId,
    string Sku,
    string VariantName,
    int ProductId,
    string ProductName,
    string? BrandName,
    string CategoryName,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    int? LowStockThreshold,
    bool IsBelowThreshold,
    DateTime UpdatedAt);

public class StockMovementQuery
{
    public int? WarehouseId { get; set; }
    public int? VariantId { get; set; }
    public string? Sku { get; set; }
    public StockMovementType? MovementType { get; set; }
    public int? UserId { get; set; }
    public Guid? TransferGroupId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record StockMovementDto(
    int Id,
    int WarehouseId,
    string WarehouseName,
    int VariantId,
    string Sku,
    string VariantName,
    string ProductName,
    StockMovementType MovementType,
    int Quantity,
    int QuantityBefore,
    int QuantityAfter,
    decimal? UnitCost,
    string? ReferenceType,
    int? ReferenceId,
    Guid? TransferGroupId,
    string? Comment,
    int? UserId,
    DateTime CreatedAt);

public class ReceiptRequest
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public string? Comment { get; set; }
    public string? DocumentNumber { get; set; }
    public decimal? UnitCost { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class IssueRequest
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public string? Comment { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class AdjustmentRequest
{
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public int TargetQuantityOnHand { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
}

public class TransferRequest
{
    public int SourceWarehouseId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public string? Comment { get; set; }
    public string? IdempotencyKey { get; set; }
}

public record StockOperationResultDto(
    int MovementId,
    Guid? TransferGroupId,
    int WarehouseId,
    int VariantId,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    int QuantityBefore,
    int QuantityAfter,
    StockMovementType MovementType,
    bool ReplayFromIdempotency);

public record TransferOperationResultDto(
    Guid TransferGroupId,
    StockOperationResultDto Outbound,
    StockOperationResultDto Inbound,
    bool ReplayFromIdempotency);

public class SetLowStockThresholdRequest
{
    public int? LowStockThreshold { get; set; }
}

public record WarehouseSummaryDto(
    int WarehouseId,
    string WarehouseName,
    int SkuCount,
    int PositiveStockSkuCount,
    int ZeroStockSkuCount,
    int LowStockSkuCount,
    int TotalUnitsOnHand);

public record MovementTypeAggregateDto(StockMovementType MovementType, int MovementCount, int TotalQuantityAbsolute);

public record StockMovementsPeriodReportDto(
    int? WarehouseId,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<MovementTypeAggregateDto> ByType);

public record InventoryValuationDto(
    int WarehouseId,
    string WarehouseName,
    decimal ValuedAmount,
    int ValuedSkuCount,
    int UnvaluedSkuCount,
    int TotalUnitsValued,
    string Notes);

public record InventoryIntegrityIssueDto(
    string Code,
    string Severity,
    int? InventoryId,
    int? WarehouseId,
    int? VariantId,
    string Message);

public record InventoryIntegrityReportDto(
    DateTime GeneratedAtUtc,
    int IssueCount,
    IReadOnlyList<InventoryIntegrityIssueDto> Issues,
    string Notes);
