using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Services;

namespace TechStore.IntegrationTests.Catalog;

internal static class CatalogTestHelper
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    public static ApplicationDbContext CreateSqlServerContext(string databaseName, bool migrate = true)
    {
        var connection =
            $"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        if (migrate)
        {
            context.Database.Migrate();
        }

        return context;
    }

    public static CategoryService CreateCategoryService(ApplicationDbContext db) => new(db);
    public static BrandService CreateBrandService(ApplicationDbContext db) => new(db);
    public static ProductService CreateProductService(ApplicationDbContext db) => new(db);
    public static AttributeDefinitionService CreateAttributeService(ApplicationDbContext db) => new(db);
}
