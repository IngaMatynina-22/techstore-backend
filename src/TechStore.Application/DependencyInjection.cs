using Microsoft.Extensions.DependencyInjection;

namespace TechStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services will be registered here as features are implemented.
        return services;
    }
}
