using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Features.Brands;
using TechStore.Domain.Common;
using TechStore.Web.ViewModels.Catalog;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
public class BrandsController : Controller
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands)
    {
        _brands = brands;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _brands.GetListAsync(includeInactive: true, cancellationToken);
        return View(items);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var item = await _brands.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [HttpGet]
    public IActionResult Create() => View(new BrandFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _brands.CreateAsync(model.ToCreateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await _brands.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : View(BrandFormViewModel.FromDetails(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BrandFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _brands.UpdateAsync(id, model.ToUpdateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Brand updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var item = await _brands.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await _brands.DeactivateAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Brand deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
