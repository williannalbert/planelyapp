using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Api.Roles;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// What a module of endpoints (<c>MapOrganization</c>, <c>MapCatalog</c>...) adds to an Api, and the rules every module follows. The
/// tests of a module are one line for each rule, so a new module gets them by naming its <c>Map</c>.
/// </summary>
internal static class EndpointModule
{
    private static List<RouteEndpoint> EndpointsOf(WebApplication app) =>
        app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    /// <summary>
    /// The endpoints that <paramref name="map"/> adds. The Api of a test already has endpoints of its own (the health checks), so they
    /// are found first in an Api that maps nothing, and left out.
    /// </summary>
    public static async Task<List<RouteEndpoint>> MappedAsync(Action<WebApplication> map)
    {
        await using var bare = await StaffApi.StartAsync(StaffApi.As(UserRole.CompanyAdmin), _ => { });
        var infrastructure = EndpointsOf(bare).Select(endpoint => endpoint.DisplayName).ToHashSet();

        await using var app = await StaffApi.StartAsync(StaffApi.As(UserRole.CompanyAdmin), map);
        var module = EndpointsOf(app).Where(endpoint => !infrastructure.Contains(endpoint.DisplayName)).ToList();

        module.ShouldNotBeEmpty("The module mapped nothing, or the data source of the endpoints is not the one of the app.");

        return module;
    }

    public static string NameOf(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? $"(no name) {endpoint.DisplayName}";

    /// <summary>Without a rule the endpoint is closed at run time (403 and an error in the log). This moves the mistake to the tests.</summary>
    public static void EveryEndpointShouldNameItsRoles(IEnumerable<RouteEndpoint> endpoints)
    {
        foreach (var endpoint in endpoints)
        {
            endpoint.Metadata.GetMetadata<RoleRule>().ShouldNotBeNull($"{NameOf(endpoint)} has no role rule.");
        }
    }

    public static void EveryEndpointShouldHaveAName(IEnumerable<RouteEndpoint> endpoints)
    {
        var names = endpoints.Select(NameOf).ToList();

        names.ShouldAllBe(name => !name.StartsWith("(no name)", StringComparison.Ordinal));

        var repeated = names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        repeated.ShouldBeEmpty();
    }

    /// <summary>
    /// The tenancy looks at the companyId of the route. A route without it would not be checked against the company of the user, so
    /// the endpoints that are not inside <c>/companies/{companyId}</c> are the ones the test names, and no other.
    /// </summary>
    public static void OnlyTheseShouldBeOutsideACompany(IEnumerable<RouteEndpoint> endpoints, params string[] allowed)
    {
        var outside = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/companies/{companyId:guid}", StringComparison.Ordinal) != true)
            .Select(NameOf)
            .ToList();

        outside.ShouldBe(allowed);
    }
}
