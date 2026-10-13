namespace TechStore.Domain.Entities;

public class OrderOperationIdempotency
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
