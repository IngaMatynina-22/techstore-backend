using System.Text;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Imports;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.Products;
using TechStore.Domain.Enums;
using TechStore.Infrastructure.Services;
using TechStore.IntegrationTests.Catalog;

namespace TechStore.IntegrationTests.Imports;

public class SupplierInventoryImportTests
{
    [Fact]
    public async Task ImportInventory_UpdatesStockAndOptionalCatalogFields()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var inventory = new InventoryService(db);
        var categoryService = new CategoryService(db);
        var productService = new ProductService(db);
        var warehouseService = new WarehouseService(db);
        var category = await categoryService.CreateAsync(new CreateCategoryRequest
        {
            Name = "Import category",
            Slug = "import-category"
        });
        var product = await productService.CreateAsync(new CreateProductRequest
        {
            Name = "Old product name",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "SUP-" + Guid.NewGuid().ToString("N")[..8],
                    VariantName = "Old variant",
                    PurchasePrice = 10,
                    SellingPrice = 20
                }
            ]
        }, null);
        var variant = (await productService.GetByIdAsync(product.Value!))!.Variants[0];
        var warehouse = await warehouseService.CreateAsync(new CreateWarehouseRequest
        {
            Name = "Import warehouse",
            Code = "IMP-" + Guid.NewGuid().ToString("N")[..6]
        });

        var csv = string.Join('\n',
            "Sku,WarehouseCode,QuantityOnHand,ProductName,VariantName,PurchasePrice,SellingPrice",
            $"{variant.Sku},{warehouse.Value!.Code},7,Updated product,Updated variant,12.50,25.00");
        var service = new CatalogImportService(db, inventory);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportInventoryAsync(stream, "supplier.csv", stream.Length, 42);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportType.Inventory.ToString(), result.Value!.ImportType);
        Assert.Equal(1, result.Value.SuccessfulRows);
        Assert.Equal(0, result.Value.FailedRows);
        Assert.Equal("Success", result.Value.Rows.Single().Status);

        var stock = await inventory.GetAsync(warehouse.Value.Id, variant.Id);
        Assert.Equal(7, stock!.QuantityOnHand);
        var storedVariant = await db.ProductVariants.Include(v => v.Product).SingleAsync(v => v.Id == variant.Id);
        Assert.Equal("Updated product", storedVariant.Product.Name);
        Assert.Equal("Updated variant", storedVariant.VariantName);
        Assert.Equal(12.50m, storedVariant.PurchasePrice);
        Assert.Equal(25.00m, storedVariant.SellingPrice);
        Assert.Equal(1, await db.StockMovements.CountAsync(m => m.ReferenceType == "InventoryImport"));
    }
}
