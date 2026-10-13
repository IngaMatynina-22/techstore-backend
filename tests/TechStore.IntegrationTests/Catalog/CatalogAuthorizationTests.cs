using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Infrastructure.Persistence;

namespace TechStore.IntegrationTests.Catalog;

public class CatalogAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:SeedOnStartup", "false");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=TechStore_AuthTests;Trusted_Connection=True;TrustServerCertificate=True");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("auth-tests-" + Guid.NewGuid()));
            });
        });
    }

    [Theory]
    [InlineData("/api/categories")]
    [InlineData("/api/brands")]
    [InlineData("/api/products")]
    [InlineData("/api/attributes?categoryId=1")]
    public async Task CatalogEndpoints_Return401ForAnonymous(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
