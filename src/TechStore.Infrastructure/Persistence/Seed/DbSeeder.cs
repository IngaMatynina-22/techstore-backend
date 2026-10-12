using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Identity;

namespace TechStore.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        try
        {
            var context = sp.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync(cancellationToken);

            await SeedRolesAsync(sp, cancellationToken);
            await SeedOrderStatusesAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var roleName in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create role '{roleName}': {errors}");
                }
            }
        }
    }

    public static async Task SeedOrderStatusesAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[]
        {
            new OrderStatus { Name = "New", Code = "New", Description = "Order has been created.", SortOrder = 1, IsFinal = false },
            new OrderStatus { Name = "Confirmed", Code = "Confirmed", Description = "Order has been confirmed by a manager.", SortOrder = 2, IsFinal = false },
            new OrderStatus { Name = "Processing", Code = "Processing", Description = "Order is being prepared.", SortOrder = 3, IsFinal = false },
            new OrderStatus { Name = "ReadyForShipment", Code = "ReadyForShipment", Description = "Order is ready for shipment.", SortOrder = 4, IsFinal = false },
            new OrderStatus { Name = "Shipped", Code = "Shipped", Description = "Order has been shipped.", SortOrder = 5, IsFinal = false },
            new OrderStatus { Name = "Delivered", Code = "Delivered", Description = "Order has been delivered.", SortOrder = 6, IsFinal = true },
            new OrderStatus { Name = "Completed", Code = "Completed", Description = "Order is completed.", SortOrder = 7, IsFinal = true },
            new OrderStatus { Name = "Cancelled", Code = "Cancelled", Description = "Order has been cancelled.", SortOrder = 8, IsFinal = true },
            new OrderStatus { Name = "Returned", Code = "Returned", Description = "Order has been returned.", SortOrder = 9, IsFinal = true }
        };

        var existingCodes = await context.OrderStatuses
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var missing = statuses.Where(s => !existingCodes.Contains(s.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        context.OrderStatuses.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }
}
