using Microsoft.EntityFrameworkCore;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Persistence.Seed;

namespace TechStore.IntegrationTests.Persistence;

public class ApplicationDbContextTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CanPersistProductVariantWithDecimalPrices()
    {
        await using var context = CreateContext();

        var category = new Category
        {
            Name = "Phones",
            Slug = "phones",
            CreatedAt = DateTime.UtcNow
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Demo Phone",
            Slug = "demo-phone",
            CreatedAt = DateTime.UtcNow
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = "SKU-001",
            VariantName = "128GB Black",
            PurchasePrice = 100.50m,
            SellingPrice = 149.99m,
            CreatedAt = DateTime.UtcNow
        };
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();

        var loaded = await context.ProductVariants.SingleAsync(x => x.Sku == "SKU-001");
        Assert.Equal(149.99m, loaded.SellingPrice);
    }

    [Fact]
    public async Task SeedOrderStatuses_IsIdempotent()
    {
        await using var context = CreateContext();

        await DbSeeder.SeedOrderStatusesAsync(context);
        await DbSeeder.SeedOrderStatusesAsync(context);

        var count = await context.OrderStatuses.CountAsync();
        Assert.Equal(9, count);
        Assert.Contains(await context.OrderStatuses.ToListAsync(), x => x.Code == "New");
    }
}
