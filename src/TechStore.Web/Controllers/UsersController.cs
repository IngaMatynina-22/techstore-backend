using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common;
using TechStore.Domain.Common;
using TechStore.Infrastructure.Identity;

namespace TechStore.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);

        var q = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u =>
                (u.Email != null && u.Email.Contains(s)) ||
                u.FirstName.Contains(s) ||
                u.LastName.Contains(s) ||
                (u.UserName != null && u.UserName.Contains(s)));
        }

        var total = await q.CountAsync(cancellationToken);
        var users = await q
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserListItemDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(ToDto(user, roles));
        }

        return Ok(new PagedResult<UserListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(ToDto(user, roles));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var roles = NormalizeRoles(request.Roles);
        if (roles.Count == 0)
        {
            return BadRequest(new { message = "At least one role is required." });
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", create.Errors.Select(e => e.Description)) });
        }

        var roleResult = await _userManager.AddToRolesAsync(user, roles);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", roleResult.Errors.Select(e => e.Description)) });
        }

        var assigned = await _userManager.GetRolesAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user, assigned));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = NormalizeRoles(request.Roles);
        if (roles.Count == 0)
        {
            return BadRequest(new { message = "At least one role is required." });
        }

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailOwner = await _userManager.FindByEmailAsync(request.Email);
            if (emailOwner is not null && emailOwner.Id != user.Id)
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            user.Email = request.Email;
            user.UserName = request.Email;
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", update.Errors.Select(e => e.Description)) });
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwd = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!pwd.Succeeded)
            {
                return BadRequest(new { message = string.Join("; ", pwd.Errors.Select(e => e.Description)) });
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        if (toRemove.Count > 0)
        {
            var rem = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!rem.Succeeded)
            {
                return BadRequest(new { message = string.Join("; ", rem.Errors.Select(e => e.Description)) });
            }
        }

        if (toAdd.Count > 0)
        {
            var add = await _userManager.AddToRolesAsync(user, toAdd);
            if (!add.Succeeded)
            {
                return BadRequest(new { message = string.Join("; ", add.Errors.Select(e => e.Description)) });
            }
        }

        var assigned = await _userManager.GetRolesAsync(user);
        return Ok(ToDto(user, assigned));
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (CurrentUserId == user.Id)
        {
            return BadRequest(new { message = "You cannot deactivate your own account." });
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (CurrentUserId == user.Id)
        {
            return BadRequest(new { message = "You cannot delete your own account." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }

    private static UserListItemDto ToDto(ApplicationUser user, IList<string> roles) =>
        new()
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            Roles = roles.ToArray(),
            CreatedAt = user.CreatedAt,
        };

    private static List<string> NormalizeRoles(IEnumerable<string>? roles) =>
        (roles ?? [])
            .Select(r => r.Trim())
            .Where(r => ApplicationRoles.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public class UserListItemDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string[] Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

public class UpdateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<string> Roles { get; set; } = [];

    [MinLength(6)]
    public string? NewPassword { get; set; }
}
