using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Infrastructure.Persistence;

namespace TechStore.IntegrationTests.Inventory;

public class InventoryAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:SeedOnStartup", "false");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=TechStore_InvAuth;Trusted_Connection=True;TrustServerCertificate=True");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("inv-auth-" + Guid.NewGuid()));
            });
        });
    }

    [Theory]
    [InlineData("/api/warehouses")]
    [InlineData("/api/inventory")]
    [InlineData("/api/stock-movements")]
    public async Task InventoryEndpoints_Return401ForAnonymous(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
