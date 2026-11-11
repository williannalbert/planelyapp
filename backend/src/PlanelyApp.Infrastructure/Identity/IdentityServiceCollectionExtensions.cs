using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Identity;

namespace PlanelyApp.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Keycloak adapter as the <see cref="IIdentityProvider"/>. One HttpClient for the whole application
    /// (it is thread safe), with its connections renewed from time to time so a change of address is noticed.
    /// </summary>
    public static IServiceCollection AddKeycloakIdentityProvider(this IServiceCollection services, KeycloakOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        services.AddSingleton<IIdentityProvider>(_ =>
        {
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
            {
                BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
                Timeout = options.RequestTimeout,
            };

            return new KeycloakIdentityProvider(
                http, options, new KeycloakTokenProvider(http, options, TimeProvider.System));
        });

        return services;
    }

    /// <summary>The issuer of the access token of the customer. The options also go to the container, because the Api validates with the same ones (5.8).</summary>
    public static IServiceCollection AddCustomerTokenIssuer(this IServiceCollection services, CustomerTokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<ICustomerTokenIssuer>(new JwtCustomerTokenIssuer(options));

        return services;
    }
}