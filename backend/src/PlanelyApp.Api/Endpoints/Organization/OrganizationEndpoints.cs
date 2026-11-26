using PlanelyApp.Api.Endpoints.Organization.Branches;
using PlanelyApp.Api.Endpoints.Organization.Companies;
using PlanelyApp.Api.Endpoints.Organization.Customers;
using PlanelyApp.Api.Endpoints.Organization.Professionals;
using PlanelyApp.Api.Endpoints.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>
/// The endpoints of the Organization module. Each resource has its own folder and its own <c>Map...</c>; this is the one line that
/// <c>Program</c> knows, so a new resource is a line here and none there.
/// </summary>
internal static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganization(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCompanies();
        endpoints.MapBranches();
        endpoints.MapProfessionals();
        endpoints.MapCustomers();
        endpoints.MapUsers();

        return endpoints;
    }
}