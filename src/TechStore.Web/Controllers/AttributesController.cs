using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechStore.Application.Features.Attributes;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Products;
using TechStore.Domain.Common;
using TechStore.Domain.Enums;
using TechStore.Web.ViewModels.Catalog;

namespace TechStore.Web.Controllers;

[Authorize(Roles = $"{ApplicationRoles.Admin},{ApplicationRoles.Manager}")]
public class AttributesController : Controller
{
    private readonly IAttributeDefinitionService _attributes;
    private readonly ICategoryService _categories;
    private readonly IProductService _products;

    public AttributesController(
        IAttributeDefinitionService attributes,
        ICategoryService categories,
        IProductService products)
    {
        _attributes = attributes;
        _categories = categories;
        _products = products;
    }

    public async Task<IActionResult> Index(int? categoryId, CancellationToken cancellationToken)
    {
        var categories = await _categories.GetLookupAsync(activeOnly: false, cancellationToken);
        ViewBag.Categories = new SelectList(categories, "Id", "Name", categoryId);

        if (!categoryId.HasValue)
        {
            return View(Array.Empty<AttributeDefinitionDto>());
        }

        var items = await _attributes.GetByCategoryAsync(categoryId.Value, cancellationToken);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? categoryId, CancellationToken cancellationToken)
    {
        await PopulateCategoriesAsync(cancellationToken);
        return View(new AttributeDefinitionFormViewModel { CategoryId = categoryId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AttributeDefinitionFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(cancellationToken);
            return View(model);
        }

        var result = await _attributes.CreateAsync(model.ToCreateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateCategoriesAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Attribute definition created.";
        return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await _attributes.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        await PopulateCategoriesAsync(cancellationToken);
        return View(AttributeDefinitionFormViewModel.FromDto(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AttributeDefinitionFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(cancellationToken);
            return View(model);
        }

        var result = await _attributes.UpdateAsync(id, model.ToUpdateRequest(), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateCategoriesAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Attribute definition updated.";
        return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
    }

    [HttpGet]
    public async Task<IActionResult> VariantValues(int productId, int variantId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == variantId);
        if (product is null || variant is null)
        {
            return NotFound();
        }

        var definitions = await _attributes.GetByCategoryAsync(product.CategoryId, cancellationToken);
        var values = await _attributes.GetVariantValuesAsync(variantId, cancellationToken);

        var model = new VariantAttributeFormViewModel
        {
            ProductId = productId,
            VariantId = variantId,
            ProductName = product.Name,
            VariantName = variant.VariantName,
            Definitions = definitions,
            CurrentValues = values,
            AttributeOptions = new SelectList(definitions, "Id", "Name")
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VariantValues(VariantAttributeFormViewModel model, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(model.ProductId, cancellationToken);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == model.VariantId);
        if (product is null || variant is null)
        {
            return NotFound();
        }

        var definitions = await _attributes.GetByCategoryAsync(product.CategoryId, cancellationToken);
        model.Definitions = definitions;
        model.ProductName = product.Name;
        model.VariantName = variant.VariantName;
        model.AttributeOptions = new SelectList(definitions, "Id", "Name", model.AttributeId);
        model.CurrentValues = await _attributes.GetVariantValuesAsync(model.VariantId, cancellationToken);

        var selected = definitions.FirstOrDefault(d => d.Id == model.AttributeId);
        if (selected is null)
        {
            ModelState.AddModelError(nameof(model.AttributeId), "Attribute is required.");
            return View(model);
        }

        model.SelectedDataType = selected.DataType;
        if (selected.DataType == AttributeDataType.Select)
        {
            model.SelectOptions = new SelectList(selected.Options, "Id", "Value", model.OptionId);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _attributes.SetVariantValueAsync(
            model.VariantId,
            new SetVariantAttributeValueRequest
            {
                AttributeId = model.AttributeId,
                Value = model.Value,
                OptionId = model.OptionId
            },
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Attribute value saved.";
        return RedirectToAction(nameof(VariantValues), new { productId = model.ProductId, variantId = model.VariantId });
    }

    private async Task PopulateCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _categories.GetLookupAsync(activeOnly: false, cancellationToken);
        ViewBag.Categories = new SelectList(categories, "Id", "Name");
    }
}
