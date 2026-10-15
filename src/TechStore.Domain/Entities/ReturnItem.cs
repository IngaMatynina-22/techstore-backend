using TechStore.Domain.Common;
using TechStore.Domain.Enums;

namespace TechStore.Domain.Entities;

public class ReturnItem : BaseEntity
{
    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public int? VariantId { get; set; }
    public string SkuSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal LineTotal { get; set; }
    public string? ReasonCode { get; set; }
    public ReturnDisposition Disposition { get; set; } = ReturnDisposition.RestockSellable;

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
