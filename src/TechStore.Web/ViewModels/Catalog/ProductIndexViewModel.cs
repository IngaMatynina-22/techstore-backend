using TechStore.Application.Common;
using TechStore.Application.Features.Products;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TechStore.Web.ViewModels.Catalog;

public class ProductIndexViewModel
{
    public ProductQuery Query { get; set; } = new();
    public PagedResult<ProductListItemDto> Result { get; set; } = new();
    public SelectList? Categories { get; set; }
    public SelectList? Brands { get; set; }
}
