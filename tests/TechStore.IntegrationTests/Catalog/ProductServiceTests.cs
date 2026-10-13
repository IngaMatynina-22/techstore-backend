using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Products;

namespace TechStore.IntegrationTests.Catalog;

public class ProductServiceTests
{
    [Fact]
    public async Task Create_WithVariant_Succeeds()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);

        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Laptops", Slug = "laptops" });
        Assert.True(category.IsSuccess);

        var result = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Demo Laptop",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    VariantName = "16GB / 512GB",
                    Sku = "LAP-001",
                    PurchasePrice = 500,
                    SellingPrice = 799.99m
                }
            ]
        }, userId: null);

        Assert.True(result.IsSuccess);
        var details = await products.GetByIdAsync(result.Value!);
        Assert.NotNull(details);
        Assert.Single(details!.Variants);
        Assert.Equal("LAP-001", details.Variants[0].Sku);
        Assert.Equal(799.99m, details.Variants[0].SellingPrice);
    }

    [Fact]
    public async Task Create_NegativePrice_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Acc", Slug = "acc" });

        var result = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Bad",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "BAD-1",
                    VariantName = "Default",
                    PurchasePrice = -1,
                    SellingPrice = 10
                }
            ]
        }, null);

        Assert.True(result.IsFailure);
        Assert.Contains("price", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DuplicateSku_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Cat", Slug = "cat" });

        var first = await products.CreateAsync(new CreateProductRequest
        {
            Name = "P1",
            CategoryId = category.Value!,
            Variants = [new ProductVariantInput { Sku = "SKU-X", VariantName = "A", PurchasePrice = 1, SellingPrice = 2 }]
        }, null);
        Assert.True(first.IsSuccess);

        var second = await products.CreateAsync(new CreateProductRequest
        {
            Name = "P2",
            CategoryId = category.Value!,
            Variants = [new ProductVariantInput { Sku = "SKU-X", VariantName = "B", PurchasePrice = 1, SellingPrice = 2 }]
        }, null);

        Assert.True(second.IsFailure);
        Assert.Contains("SKU", second.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPaged_SearchFilterAndPagination_WorkOnServerSide()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Phones", Slug = "phones-2" });

        for (var i = 1; i <= 5; i++)
        {
            var create = await products.CreateAsync(new CreateProductRequest
            {
                Name = $"Phone {i}",
                CategoryId = category.Value!,
                Variants =
                [
                    new ProductVariantInput
                    {
                        Sku = $"PH-{i:000}",
                        VariantName = "Default",
                        PurchasePrice = 100 + i,
                        SellingPrice = 200 + i
                    }
                ]
            }, null);
            Assert.True(create.IsSuccess);
        }

        var page = await products.GetPagedAsync(new ProductQuery
        {
            Search = "PH-003",
            CategoryId = category.Value,
            Page = 1,
            PageSize = 2,
            SortBy = ProductSortBy.NameAsc
        });

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("Phone 3", page.Items[0].Name);

        var filtered = await products.GetPagedAsync(new ProductQuery
        {
            CategoryId = category.Value,
            Page = 2,
            PageSize = 2,
            SortBy = ProductSortBy.NameAsc
        });

        Assert.Equal(5, filtered.TotalCount);
        Assert.Equal(2, filtered.Items.Count);
        Assert.Equal(3, filtered.TotalPages);
    }
}
