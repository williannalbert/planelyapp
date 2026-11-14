using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PlanelyApp.Application.Abstractions.Identity;
using Polly;

namespace PlanelyApp.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    internal const string KeycloakClientName = "keycloak";

    /// <summary>
    /// Registers the Keycloak adapter as the <see cref="IIdentityProvider"/>. The HttpClient is the named client
    /// <c>keycloak</c> with a resilience pipeline: a total and a per-attempt timeout, retry only for reads (a POST or PUT
    /// that is repeated could create two accounts or send two e-mails) and a circuit breaker that fails fast while
    /// Keycloak is down. The adapter takes the client once, so there is one for the whole application; its connections
    /// are renewed from time to time so a change of address is noticed.
    /// </summary>
    public static IServiceCollection AddKeycloakIdentityProvider(this IServiceCollection services, KeycloakOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var client = services
            .AddHttpClient(KeycloakClientName, http =>
            {
                http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

                // A safety net only: the pipeline cuts the call before this.
                http.Timeout = options.RequestTimeout + TimeSpan.FromSeconds(5);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });

        client.AddStandardResilienceHandler(resilience =>
        {
            resilience.TotalRequestTimeout.Timeout = options.RequestTimeout;
            resilience.AttemptTimeout.Timeout = options.AttemptTimeout;

            resilience.Retry.MaxRetryAttempts = 2;
            resilience.Retry.Delay = TimeSpan.FromMilliseconds(300);
            resilience.Retry.BackoffType = DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.DisableForUnsafeHttpMethods();

            resilience.CircuitBreaker.MinimumThroughput = 5;
            resilience.CircuitBreaker.FailureRatio = 0.5;
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

            // Polly needs a window of at least twice the attempt timeout.
            resilience.CircuitBreaker.SamplingDuration =
                TimeSpan.FromSeconds(Math.Max(30, 2 * options.AttemptTimeout.TotalSeconds));
        });

        services.AddSingleton<IIdentityProvider>(provider =>
        {
            var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(KeycloakClientName);

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