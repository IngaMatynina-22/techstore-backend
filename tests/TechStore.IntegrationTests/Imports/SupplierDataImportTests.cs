using System.Text;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Imports;
using TechStore.Application.Features.Products;
using TechStore.Application.Features.Suppliers;
using TechStore.Infrastructure.Services;
using TechStore.IntegrationTests.Catalog;

namespace TechStore.IntegrationTests.Imports;

public class SupplierDataImportTests
{
    [Fact]
    public async Task ImportAndExport_RoundTripsSupplierAndProductDetails()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var categories = new CategoryService(db);
        var products = new ProductService(db);
        var category = await categories.CreateAsync(new CreateCategoryRequest
        {
            Name = "Supplier import category",
            Slug = "supplier-import-" + Guid.NewGuid().ToString("N")[..8]
        });
        var product = await products.CreateAsync(new CreateProductRequest
        {
            Name = "Supplier import product",
            CategoryId = category.Value!,
            Variants =
            [
                new ProductVariantInput
                {
                    Sku = "SUP-" + Guid.NewGuid().ToString("N")[..8],
                    VariantName = "Default",
                    PurchasePrice = 10,
                    SellingPrice = 20
                }
            ]
        }, null);
        var sku = (await products.GetByIdAsync(product.Value!))!.Variants.Single().Sku;
        var csv = string.Join('\n',
            "SupplierId,SupplierName,ContactPerson,Email,Phone,Address,TaxNumber,Notes,IsActive,ProductSku,SupplierSku,SupplyPrice,DeliveryDays,IsPreferred",
            $",Acme Supplies,Alex,alex@example.test,+380000000000,Kyiv,12345678,\"Preferred\npartner\",true,{sku},ACME-1,12.50,4,true");
        var importService = new SupplierDataImportService(db);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await importService.ImportAsync(stream, "suppliers.csv", stream.Length, 42);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CreatedSuppliers);
        Assert.Equal(1, result.Value.UpsertedProducts);
        Assert.Equal("Success", result.Value.Rows.Single().Status);
        var supplier = await db.Suppliers.SingleAsync();
        Assert.Equal("Acme Supplies", supplier.Name);
        Assert.Equal("alex@example.test", supplier.Email);
        var link = await db.SupplierProducts.SingleAsync();
        Assert.Equal(sku, (await db.ProductVariants.SingleAsync(v => v.Id == link.VariantId)).Sku);
        Assert.Equal("ACME-1", link.SupplierSku);
        Assert.Equal(12.50m, link.SupplyPrice);
        Assert.Equal(4, link.DeliveryDays);
        Assert.True(link.IsPreferred);

        var export = await new CatalogExportService(db).ExportSuppliersAsync();
        Assert.True(export.IsSuccess);
        var exportedCsv = Encoding.UTF8.GetString(export.Value!.Content);
        Assert.Contains("SupplierId,SupplierName,ContactPerson", exportedCsv);
        Assert.Contains("Acme Supplies", exportedCsv);
        Assert.Contains("ACME-1", exportedCsv);

        await using var reimportStream = new MemoryStream(export.Value.Content);
        var reimported = await importService.ImportAsync(
            reimportStream,
            "suppliers-export.csv",
            reimportStream.Length,
            42);
        Assert.True(reimported.IsSuccess);
        Assert.Equal(1, reimported.Value!.UpdatedSuppliers);
        Assert.Equal("Preferred\npartner", (await db.Suppliers.SingleAsync()).Notes);
    }

    [Fact]
    public async Task Import_WithUnknownProductSkuDoesNotCreateSupplier()
    {
        await using var db = CatalogTestHelper.CreateContext();
        var csv = string.Join('\n',
            "SupplierId,SupplierName,ContactPerson,Email,Phone,Address,TaxNumber,Notes,IsActive,ProductSku,SupplierSku,SupplyPrice,DeliveryDays,IsPreferred",
            ",Unknown supplier,,,,,,,true,MISSING-SKU,,1,,false");
        var importService = new SupplierDataImportService(db);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await importService.ImportAsync(stream, "suppliers.csv", stream.Length, 42);

        Assert.True(result.IsFailure);
        Assert.Contains("Product SKU 'MISSING-SKU' was not found", result.Error);
        Assert.Empty(await db.Suppliers.ToListAsync());
    }
}
