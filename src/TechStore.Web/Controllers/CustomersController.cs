using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Application.Features.Customers;
using TechStore.Domain.Common;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/customers")]
public class CustomersController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public CustomersController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] CustomerQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);

        var q = _db.Customers.AsNoTracking().AsQueryable();
        if (!query.IncludeInactive)
        {
            q = q.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c =>
                c.FirstName.Contains(s) ||
                c.LastName.Contains(s) ||
                c.Email.Contains(s) ||
                (c.Phone != null && c.Phone.Contains(s)));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                IsActive = c.IsActive,
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<CustomerListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                IsActive = c.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }
}
