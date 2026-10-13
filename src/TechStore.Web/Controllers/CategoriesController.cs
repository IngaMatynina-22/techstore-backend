using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechStore.Application.Features.Categories;
using TechStore.Domain.Common;
using TechStore.Web.ViewModels.Catalog;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
public class CategoriesController : Controller
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _categories.GetListAsync(includeInactive: true, cancellationToken);
        return View(items);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var item = await _categories.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateParentsAsync(null, cancellationToken);
        return View(new CategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentsAsync(null, cancellationToken);
            return View(model);
        }

        var result = await _categories.CreateAsync(model.ToCreateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateParentsAsync(null, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await _categories.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        await PopulateParentsAsync(id, cancellationToken);
        return View(CategoryFormViewModel.FromDetails(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateParentsAsync(id, cancellationToken);
            return View(model);
        }

        var result = await _categories.UpdateAsync(id, model.ToUpdateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateParentsAsync(id, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var item = await _categories.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpPost, ActionName("Deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await _categories.DeactivateAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Category deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateParentsAsync(int? excludeId, CancellationToken cancellationToken)
    {
        var parents = await _categories.GetLookupAsync(activeOnly: false, cancellationToken);
        if (excludeId.HasValue)
        {
            parents = parents.Where(p => p.Id != excludeId.Value).ToList();
        }

        ViewBag.ParentOptions = new SelectList(parents, "Id", "Name");
    }
}
