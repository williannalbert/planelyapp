using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Roles;
using PlanelyApp.Api.Tenancy;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The Api of a signed-in staff member: the real authentication, tenancy and role checks, in this order, around the endpoints a
/// test maps, and a <see cref="FakeDispatcher"/> in place of the Application. Used by the tests of the endpoints.
/// </summary>
internal static class StaffApi
{
    /// <summary>The company of every actor made here (the platform administrator has none).</summary>
    public static readonly Guid Company = Guid.CreateVersion7();

    public static StaffActor Actor(UserRole role, Guid? company = null) => role == UserRole.PlatformAdmin
        ? new StaffActor(Guid.CreateVersion7(), null, role, [])
        : new StaffActor(Guid.CreateVersion7(), company ?? Company, role, null);

    /// <summary>A dispatcher in which the identity of the token belongs to a user with this role.</summary>
    public static FakeDispatcher As(UserRole role, Guid? company = null) =>
        new() { Resolve = _ => Actor(role, company) };

    public static Task<WebApplication> StartAsync(
        FakeDispatcher dispatcher,
        Action<WebApplication> map,
        Action<WebApplicationBuilder>? configure = null,
        string environment = "Production") => TestApi.StartAsync(
        api =>
        {
            api.UseApiAuthentication();
            api.UseApiTenancy();
            api.UseApiRoles();
            map(api);
        },
        builder =>
        {
            builder.Services.AddSingleton<IDispatcher>(dispatcher);
            builder.Services.AddScoped<TenantContext>();
            builder.Services.AddApiAuthentication(TestIdentity.Keycloak, builder.Environment);
            builder.Services.Configure<JwtBearerOptions>(
                ApiAuthenticationExtensions.StaffScheme, options => options.ConfigurationManager = TestIdentity.Metadata);
            configure?.Invoke(builder);
        },
        environment);

    public static Task<HttpResponseMessage> SendAsync(
        WebApplication app,
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        HttpContent? content = null,
        bool signedIn = true,
        string? language = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };

        if (signedIn)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestIdentity.Token());
        }

        if (language is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
        }

        return app.GetTestClient().SendAsync(request, cancellationToken);
    }

    public static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");
}
