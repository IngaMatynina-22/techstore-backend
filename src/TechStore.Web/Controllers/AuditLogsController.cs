using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Audit;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/audit-logs")]
public class AuditLogsController : ApiControllerBase
{
    private readonly IAuditQueryService _audit;

    public AuditLogsController(IAuditQueryService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
        => Ok(await _audit.GetPagedAsync(query, cancellationToken));
}
