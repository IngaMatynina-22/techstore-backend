using Microsoft.EntityFrameworkCore;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Services;

namespace TechStore.IntegrationTests.Catalog;

internal static class CatalogTestHelper
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public static CategoryService CreateCategoryService(ApplicationDbContext db) => new(db);
    public static BrandService CreateBrandService(ApplicationDbContext db) => new(db);
    public static ProductService CreateProductService(ApplicationDbContext db) => new(db);
    public static AttributeDefinitionService CreateAttributeService(ApplicationDbContext db) => new(db);
}
