using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Application.Features.Attributes;
using TechStore.Application.Features.Audit;
using TechStore.Application.Features.Brands;
using TechStore.Application.Features.Categories;
using TechStore.Application.Features.Exports;
using TechStore.Application.Features.Imports;
using TechStore.Application.Features.Inventory;
using TechStore.Application.Features.InventoryCounts;
using TechStore.Application.Features.Orders;
using TechStore.Application.Features.Products;
using TechStore.Application.Features.Purchasing;
using TechStore.Application.Features.Reports;
using TechStore.Application.Features.Returns;
using TechStore.Application.Features.Settings;
using TechStore.Application.Features.Suppliers;
using TechStore.Infrastructure.Identity;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Services;

namespace TechStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5);
            }));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAttributeDefinitionService, AttributeDefinitionService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<ICatalogImportService, CatalogImportService>();
        services.AddScoped<ICatalogExportService, CatalogExportService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IInventoryCountService, InventoryCountService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();

        return services;
    }
}
