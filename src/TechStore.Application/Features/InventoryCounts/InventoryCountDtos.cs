using TechStore.Domain.Enums;

namespace TechStore.Application.Features.InventoryCounts;

public class CreateInventoryCountRequest
{
    public int WarehouseId { get; set; }
    public string? Notes { get; set; }
}

public class SetCountActualsRequest
{
    public List<CountActualLine> Lines { get; set; } = [];
}

public class CountActualLine
{
    public int LineId { get; set; }
    public int ActualQuantity { get; set; }
}

public record InventoryCountLineDto(
    int Id,
    int VariantId,
    string Sku,
    int InventoryItemId,
    int ExpectedQuantity,
    int? ActualQuantity,
    int? Difference,
    int? AppliedMovementId);

public record InventoryCountSessionDto(
    int Id,
    string SessionNumber,
    int WarehouseId,
    string WarehouseName,
    InventoryCountStatus Status,
    DateTime? SnapshotTakenAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    bool AdjustmentsApplied,
    string? Notes,
    IReadOnlyList<InventoryCountLineDto> Lines);
