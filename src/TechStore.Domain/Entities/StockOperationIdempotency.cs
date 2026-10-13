using TechStore.Domain.Common;

namespace TechStore.Domain.Entities;

/// <summary>
/// Stores idempotency keys for stock-mutating operations to prevent duplicate side effects.
/// </summary>
public class StockOperationIdempotency : BaseEntity
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
