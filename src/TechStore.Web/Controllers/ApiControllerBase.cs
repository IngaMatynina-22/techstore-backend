using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common;

namespace TechStore.Web.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return ProblemFromResult(result.Error!, result.Code);
    }

    protected IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ProblemFromResult(result.Error!, result.Code);
    }

    protected IActionResult CreatedFromResult<T>(Result<T> result, string actionName, object? routeValues)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValues, result.Value);
        }

        return ProblemFromResult(result.Error!, result.Code);
    }

    protected ObjectResult ProblemFromResult(string error, string? code)
    {
        var status = code switch
        {
            ResultCodes.NotFound => StatusCodes.Status404NotFound,
            ResultCodes.Forbidden => StatusCodes.Status403Forbidden,
            ResultCodes.Conflict or ResultCodes.InsufficientStock or ResultCodes.ConcurrencyConflict
                or ResultCodes.IdempotencyConflict or ResultCodes.InvalidTransition => StatusCodes.Status409Conflict,
            ResultCodes.NoChange => StatusCodes.Status200OK,
            _ => StatusCodes.Status400BadRequest
        };

        if (code == ResultCodes.NoChange)
        {
            return Ok(new { code, message = error });
        }

        return Problem(
            detail: error,
            statusCode: status,
            title: code ?? ResultCodes.Validation,
            type: $"https://httpstatuses.com/{status}");
    }

    protected int? CurrentUserId
    {
        get
        {
            var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
