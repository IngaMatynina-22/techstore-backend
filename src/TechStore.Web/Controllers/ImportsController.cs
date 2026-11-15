using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Imports;
using TechStore.Application.Features.Suppliers;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/imports")]
public class ImportsController : ApiControllerBase
{
    private readonly ICatalogImportService _imports;
    private readonly ISupplierDataImportService _supplierImports;

    public ImportsController(ICatalogImportService imports, ISupplierDataImportService supplierImports)
    {
        _imports = imports;
        _supplierImports = supplierImports;
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

    [HttpPost("inventory")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> ImportInventory(
        IFormFile file,
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
        var result = await _imports.ImportInventoryAsync(
            stream,
            file.FileName,
            file.Length,
            CurrentUserId.Value,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : FromResult(result);
    }

    [HttpPost("suppliers")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> ImportSuppliers(
        IFormFile file,
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
        var result = await _supplierImports.ImportAsync(
            stream,
            file.FileName,
            file.Length,
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
