using TechStore.Infrastructure.Persistence.Seed;

namespace TechStore.Web.Extensions;

public static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var seedOnStartup = app.Configuration.GetValue("Database:SeedOnStartup", true);
        if (!seedOnStartup)
        {
            return;
        }

        try
        {
            await DbSeeder.SeedAsync(app.Services);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
            logger.LogError(ex, "Database migration/seed failed. Ensure SQL Server is running and the connection string is valid.");

            if (!app.Environment.IsDevelopment())
            {
                throw;
            }
        }
    }
}
