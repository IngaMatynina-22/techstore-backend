using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Persistence.Seed;

namespace TechStore.IntegrationTests.Orders;

public class OrderAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName = "ord-auth-" + Guid.NewGuid();

    public OrderAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        var dbName = _dbName;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:SeedOnStartup", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
            });
        });
    }

    [Fact]
    public async Task Anonymous_Gets401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_Gets403_OnStaffOnlyConfirm()
    {
        var client = await CreateAuthenticatedClientAsync(ApplicationRoles.Customer, "cust@test.local");
        var response = await client.PostAsync("/api/orders/1/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_Gets403_OnInventory()
    {
        var client = await CreateAuthenticatedClientAsync(ApplicationRoles.Customer, "cust2@test.local");
        var response = await client.GetAsync("/api/inventory");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_CanAccessOrdersList()
    {
        var client = await CreateAuthenticatedClientAsync(ApplicationRoles.Manager, "mgr@test.local");
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role, string email)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            await DbSeeder.SeedRolesAsync(scope.ServiceProvider);
            await DbSeeder.SeedOrderStatusesAsync(db);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "T",
                    LastName = "U",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var create = await userManager.CreateAsync(user, "ChangeMe123!");
                Assert.True(create.Succeeded, string.Join("; ", create.Errors.Select(e => e.Description)));
                await userManager.AddToRoleAsync(user, role);

                if (role == ApplicationRoles.Customer)
                {
                    db.Customers.Add(new Customer
                    {
                        UserId = user.Id,
                        FirstName = "T",
                        LastName = "U",
                        Email = email,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/account/login", new
        {
            email,
            password = "ChangeMe123!",
            rememberMe = false
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
