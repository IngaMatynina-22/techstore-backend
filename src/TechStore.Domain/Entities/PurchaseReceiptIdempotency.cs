namespace TechStore.Domain.Entities;

public class PurchaseReceiptIdempotency
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int PurchaseOrderId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
