using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;

namespace PlanelyApp.Infrastructure;

public static class DependencyInjection
{
    // Fixed on purpose: ServerVersion.AutoDetect would open a connection while the services are registered.
    // Keep it equal to the image in infra/docker-compose.yml.
    private static readonly ServerVersion DatabaseVersion = new MySqlServerVersion(new Version(8, 4, 0));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PlanelyDbContext>(options => options.UseMySql(connectionString, DatabaseVersion));

        // The use cases ask for IUnitOfWork and get the same context the repositories use.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PlanelyDbContext>());

        return services;
    }
}