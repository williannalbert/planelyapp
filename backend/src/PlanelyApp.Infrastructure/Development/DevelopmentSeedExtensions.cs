using Microsoft.Extensions.DependencyInjection;

namespace PlanelyApp.Infrastructure.Development;

public static class DevelopmentSeedExtensions
{
    /// <summary>Only for Development: the Api calls it inside <c>if (IsDevelopment())</c>, so no other environment even has the code registered.</summary>
    public static IServiceCollection AddDevelopmentSeed(this IServiceCollection services, DevelopmentSeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        services.AddSingleton(options);
        services.AddScoped<PlatformAdminSeed>();
        services.AddScoped<DevelopmentCompanySeed>();
        services.AddHostedService<DevelopmentSeedService>();

        return services;
    }
}