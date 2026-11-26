using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Api.Endpoints.Organization;
using PlanelyApp.Api.Roles;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The Organization module as a whole. What each resource guarantees is in its own tests; these look at what no single resource
/// can see: a new endpoint that forgot its roles, a name used twice, a route that escaped the company of the request.
/// </summary>
public class OrganizationEndpointsTests
{
    /// <summary>The one endpoint of the module that is not inside a company: it is what creates one.</summary>
    private static readonly string[] OutsideACompany = ["RegisterCompany"];

    private static List<RouteEndpoint> EndpointsOf(WebApplication app) =>
        app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    /// <summary>
    /// What <c>MapOrganization</c> adds. The Api of a test already has endpoints of its own (the health checks), so they are
    /// found first in an Api that maps nothing, and left out.
    /// </summary>
    private static async Task<List<RouteEndpoint>> MappedAsync()
    {
        await using var bare = await StaffApi.StartAsync(StaffApi.As(UserRole.CompanyAdmin), _ => { });
        var infrastructure = EndpointsOf(bare).Select(endpoint => endpoint.DisplayName).ToHashSet();

        await using var app = await StaffApi.StartAsync(StaffApi.As(UserRole.CompanyAdmin), api => api.MapOrganization());
        var module = EndpointsOf(app).Where(endpoint => !infrastructure.Contains(endpoint.DisplayName)).ToList();

        module.ShouldNotBeEmpty("MapOrganization mapped nothing, or the data source of the endpoints is not the one of the app.");

        return module;
    }

    private static string NameOf(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? $"(no name) {endpoint.DisplayName}";

    [Fact]
    public async Task Every_endpoint_of_the_module_should_name_the_roles_that_may_call_it()
    {
        var endpoints = await MappedAsync();

        // Without a rule the endpoint is closed at run time (403 and an error in the log). This moves the mistake to the tests.
        foreach (var endpoint in endpoints)
        {
            endpoint.Metadata.GetMetadata<RoleRule>().ShouldNotBeNull($"{NameOf(endpoint)} has no role rule.");
        }
    }

    [Fact]
    public async Task Every_endpoint_of_the_module_should_have_a_name_that_no_other_has()
    {
        var endpoints = await MappedAsync();

        var names = endpoints.Select(NameOf).ToList();

        names.ShouldAllBe(name => !name.StartsWith("(no name)", StringComparison.Ordinal));
        var repeated = names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        repeated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_the_registration_of_a_company_should_be_outside_a_company()
    {
        var endpoints = await MappedAsync();

        // The tenancy looks at the companyId of the route. A route without it would not be checked against the company of the user.
        var outside = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/companies/{companyId:guid}", StringComparison.Ordinal) != true)
            .Select(NameOf)
            .ToList();

        outside.ShouldBe(OutsideACompany);
    }
}
