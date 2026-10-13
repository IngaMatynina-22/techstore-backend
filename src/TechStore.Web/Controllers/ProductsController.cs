using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechStore.Application.Features.Brands;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Products;
using TechStore.Domain.Common;
using TechStore.Web.ViewModels.Catalog;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
public class ProductsController : Controller
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;
    private readonly IBrandService _brands;

    public ProductsController(
        IProductService products,
        ICategoryService categories,
        IBrandService brands)
    {
        _products = products;
        _categories = categories;
        _brands = brands;
    }

    public async Task<IActionResult> Index(ProductQuery query, CancellationToken cancellationToken)
    {
        var result = await _products.GetPagedAsync(query, cancellationToken);
        var model = new ProductIndexViewModel
        {
            Query = query,
            Result = result,
            Categories = await BuildCategorySelectAsync(cancellationToken),
            Brands = await BuildBrandSelectAsync(cancellationToken)
        };
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var item = await _products.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateLookupsAsync(cancellationToken);
        return View(new ProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        var result = await _products.CreateAsync(model.ToCreateRequest(), GetUserId(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await _products.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        await PopulateLookupsAsync(cancellationToken);
        return View(ProductFormViewModel.FromDetails(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        var result = await _products.UpdateAsync(id, model.ToUpdateRequest(), GetUserId(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateLookupsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var item = await _products.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await _products.DeactivateAsync(id, GetUserId(), cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Product deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AddVariant(int id, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return View(new ProductVariantFormViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(int id, ProductVariantFormViewModel model, CancellationToken cancellationToken)
    {
        model.ProductId = id;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _products.AddVariantAsync(id, model.ToInput(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Variant added.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> EditVariant(int id, int variantId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == variantId);
        if (product is null || variant is null)
        {
            return NotFound();
        }

        return View(ProductVariantFormViewModel.FromDto(product.Id, product.Name, variant));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVariant(int id, int variantId, ProductVariantFormViewModel model, CancellationToken cancellationToken)
    {
        model.ProductId = id;
        model.VariantId = variantId;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _products.UpdateVariantAsync(id, variantId, model.ToUpdateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Variant updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateLookupsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Categories = await BuildCategorySelectAsync(cancellationToken);
        ViewBag.Brands = await BuildBrandSelectAsync(cancellationToken);
    }

    private async Task<SelectList> BuildCategorySelectAsync(CancellationToken cancellationToken)
    {
        var categories = await _categories.GetLookupAsync(activeOnly: false, cancellationToken);
        return new SelectList(categories, "Id", "Name");
    }

    private async Task<SelectList> BuildBrandSelectAsync(CancellationToken cancellationToken)
    {
        var brands = await _brands.GetLookupAsync(activeOnly: false, cancellationToken);
        return new SelectList(brands, "Id", "Name");
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}
