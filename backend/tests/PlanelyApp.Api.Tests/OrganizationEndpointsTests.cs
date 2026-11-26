using Microsoft.AspNetCore.Routing;
using PlanelyApp.Api.Endpoints.Organization;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The Organization module as a whole. What each resource guarantees is in its own tests; these look at what no single resource
/// can see: a new endpoint that forgot its roles, a name used twice, a route that escaped the company of the request.
/// </summary>
public class OrganizationEndpointsTests
{
    private static Task<List<RouteEndpoint>> MappedAsync() =>
        EndpointModule.MappedAsync(api => api.MapOrganization());

    [Fact]
    public async Task Every_endpoint_of_the_module_should_name_the_roles_that_may_call_it() =>
        EndpointModule.EveryEndpointShouldNameItsRoles(await MappedAsync());

    [Fact]
    public async Task Every_endpoint_of_the_module_should_have_a_name_that_no_other_has() =>
        EndpointModule.EveryEndpointShouldHaveAName(await MappedAsync());

    [Fact]
    public async Task Only_the_registration_of_a_company_should_be_outside_a_company() =>
        EndpointModule.OnlyTheseShouldBeOutsideACompany(await MappedAsync(), "RegisterCompany");
}
