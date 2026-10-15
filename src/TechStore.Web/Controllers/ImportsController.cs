using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Imports;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/imports")]
public class ImportsController : ApiControllerBase
{
    private readonly ICatalogImportService _imports;

    public ImportsController(ICatalogImportService imports)
    {
        _imports = imports;
    }

    [HttpPost("catalog")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> ImportCatalog(
        IFormFile file,
        [FromQuery] CatalogImportMode mode = CatalogImportMode.ValidateOnly,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                detail: "CSV file is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "validation");
        }

        if (CurrentUserId is null)
        {
            return Unauthorized();
        }

        await using var stream = file.OpenReadStream();
        var result = await _imports.ImportProductsAsync(
            stream,
            file.FileName,
            file.Length,
            mode,
            CurrentUserId.Value,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : FromResult(result);
    }

    [HttpGet("{batchId:int}")]
    public async Task<IActionResult> GetBatch(int batchId, CancellationToken cancellationToken)
    {
        var batch = await _imports.GetBatchAsync(batchId, cancellationToken);
        return batch is null ? NotFound() : Ok(batch);
    }
}
