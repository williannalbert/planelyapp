using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Infrastructure.Identity;

namespace PlanelyApp.Api.Authentication;

internal static class ApiAuthenticationExtensions
{
    /// <summary>The scheme of the staff. The customer (5.8) gets a scheme of its own.</summary>
    public const string StaffScheme = "Keycloak";

    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        KeycloakOptions keycloak,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keycloak);
        ArgumentNullException.ThrowIfNull(environment);

        // The token must carry exactly this issuer, so BaseUrl is the address the people use to reach Keycloak.
        var authority = $"{keycloak.BaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(keycloak.Realm)}";

        services
            .AddAuthentication(StaffScheme)
            .AddJwtBearer(StaffScheme, options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = !environment.IsDevelopment();

                // Keeps "sub" as "sub": the middleware reads it by that name.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = keycloak.ClientId,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ClockSkew = AllowedClockSkew,
                };
            });

        // Secure by default: an endpoint with no policy of its own asks for a signed-in user. Opening one is a decision.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(StaffScheme).RequireAuthenticatedUser().Build());

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());

        return services;
    }

    /// <summary>After the exception handler (<c>UseApiBasics</c>) and before the endpoints.</summary>
    public static IApplicationBuilder UseApiAuthentication(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<CurrentUserMiddleware>();

        return app;
    }
}