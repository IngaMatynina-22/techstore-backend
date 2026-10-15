using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Audit;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Services;

public class AuditQueryService : IAuditQueryService
{
    private readonly ApplicationDbContext _db;

    public AuditQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var logs = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var type = query.EntityType.Trim();
            logs = logs.Where(l => l.EntityType == type);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var id = query.EntityId.Trim();
            logs = logs.Where(l => l.EntityId == id);
        }

        var total = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto(
                l.Id,
                l.UserId,
                l.EntityType,
                l.EntityId,
                l.Action,
                l.OldValues,
                l.NewValues,
                l.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
