using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Persistence.Interceptors;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure;

public static class DependencyInjection
{
    // Fixed on purpose: ServerVersion.AutoDetect would open a connection while the services are registered.
    // Keep it equal to the image in infra/docker-compose.yml.
    private static readonly ServerVersion DatabaseVersion = new MySqlServerVersion(new Version(8, 4, 0));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddPlanelyDbContext<PlanelyDbContext>(connectionString);

        // The use cases ask for IUnitOfWork and get the same context the repositories use.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PlanelyDbContext>());

        return services;
    }

    /// <summary>
    /// Registers a context with the interceptors and the tenant of the scope. Public so the tests can register
    /// their own context with exactly the same setup as production.
    /// </summary>
    public static IServiceCollection AddPlanelyDbContext<TContext>(this IServiceCollection services, string connectionString)
        where TContext : PlanelyDbContext
    {
        // TryAdd: a test that registered its own TimeProvider first keeps it.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditInterceptor>();
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantProvider>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<TenantWriteInterceptor>();

        services.AddDbContext<TContext>((provider, options) => options
            .UseMySql(connectionString, DatabaseVersion)
            .AddInterceptors(
                provider.GetRequiredService<TenantWriteInterceptor>(),
                provider.GetRequiredService<AuditInterceptor>()));

        return services;
    }
}