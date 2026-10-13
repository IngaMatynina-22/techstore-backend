using TechStore.Application.Features.Attributes;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Products;
using TechStore.Domain.Enums;

namespace TechStore.IntegrationTests.Catalog;

public class AttributeDefinitionServiceTests
{
    [Fact]
    public async Task CreateSelectAttribute_AndAssignVariantValue()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var attributes = CatalogTestHelper.CreateAttributeService(db);

        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "Phones", Slug = "phones-attr" });
        var attribute = await attributes.CreateAsync(new CreateAttributeDefinitionRequest
        {
            CategoryId = category.Value!,
            Name = "Color",
            Code = "color",
            DataType = AttributeDataType.Select,
            Options = ["Black", "Silver"]
        });
        Assert.True(attribute.IsSuccess);

        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Phone X",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "PX-1",
                    VariantName = "Black",
                    PurchasePrice = 100,
                    SellingPrice = 150
                }
            ]
        }, null);
        Assert.True(product.IsSuccess);

        var definition = await attributes.GetByIdAsync(attribute.Value!);
        var optionId = definition!.Options.Single(o => o.Value == "Black").Id;
        var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;

        var set = await attributes.SetVariantValueAsync(variantId, new SetVariantAttributeValueRequest
        {
            AttributeId = attribute.Value!,
            OptionId = optionId
        });

        Assert.True(set.IsSuccess);
        var values = await attributes.GetVariantValuesAsync(variantId);
        Assert.Contains(values, v => v.OptionValue == "Black");
    }

    [Fact]
    public async Task SetVariantValue_WrongCategoryAttribute_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var attributes = CatalogTestHelper.CreateAttributeService(db);

        var catA = await categories.CreateAsync(new CreateCategoryRequest { Name = "A", Slug = "cat-a" });
        var catB = await categories.CreateAsync(new CreateCategoryRequest { Name = "B", Slug = "cat-b" });

        var attribute = await attributes.CreateAsync(new CreateAttributeDefinitionRequest
        {
            CategoryId = catA.Value!,
            Name = "RAM",
            DataType = AttributeDataType.Number
        });

        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "From B",
            CategoryId = catB.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "B-1",
                    VariantName = "Default",
                    PurchasePrice = 1,
                    SellingPrice = 2
                }
            ]
        }, null);

        var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;
        var result = await attributes.SetVariantValueAsync(variantId, new SetVariantAttributeValueRequest
        {
            AttributeId = attribute.Value!,
            Value = "16"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("category", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetVariantValue_InvalidNumber_Fails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = CatalogTestHelper.CreateCategoryService(db);
        var products = CatalogTestHelper.CreateProductService(db);
        var attributes = CatalogTestHelper.CreateAttributeService(db);

        var category = await categories.CreateAsync(new CreateCategoryRequest { Name = "C", Slug = "cat-c" });
        var attribute = await attributes.CreateAsync(new CreateAttributeDefinitionRequest
        {
            CategoryId = category.Value!,
            Name = "Storage",
            DataType = AttributeDataType.Number,
            IsRequired = true
        });

        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Item",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "C-1",
                    VariantName = "Default",
                    PurchasePrice = 1,
                    SellingPrice = 2
                }
            ]
        }, null);

        var variantId = (await products.GetByIdAsync(product.Value!))!.Variants[0].Id;
        var result = await attributes.SetVariantValueAsync(variantId, new SetVariantAttributeValueRequest
        {
            AttributeId = attribute.Value!,
            Value = "not-a-number"
        });

        Assert.True(result.IsFailure);
    }
}
