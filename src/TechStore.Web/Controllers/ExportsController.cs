using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Exports;
using TechStore.Domain.Common;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
[Route("api/exports")]
public class ExportsController : ApiControllerBase
{
    private readonly ICatalogExportService _exports;

    public ExportsController(ICatalogExportService exports)
    {
        _exports = exports;
    }

    [HttpGet("products")]
    public async Task<IActionResult> ExportProducts(CancellationToken cancellationToken)
    {
        var result = await _exports.ExportProductsAsync(cancellationToken);
        return result.IsSuccess
            ? File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
            : FromResult(result);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> ExportInventory(
        [FromQuery] int? warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _exports.ExportInventoryAsync(warehouseId, cancellationToken);
        return result.IsSuccess
            ? File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
            : FromResult(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> ExportOrders(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var result = await _exports.ExportOrdersAsync(fromUtc, toUtc, cancellationToken);
        return result.IsSuccess
            ? File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
            : FromResult(result);
    }
}
