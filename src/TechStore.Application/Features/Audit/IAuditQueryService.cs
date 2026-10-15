using TechStore.Application.Common;
using TechStore.Domain.Enums;

namespace TechStore.Application.Features.Audit;

public record AuditLogDto(
    int Id,
    int? UserId,
    string EntityType,
    string EntityId,
    AuditAction Action,
    string? OldValues,
    string? NewValues,
    DateTime CreatedAt);

public class AuditLogQuery
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
