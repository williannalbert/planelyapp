using PlanelyApp.Api.Endpoints.Catalog.ServiceCategories;
using PlanelyApp.Api.Endpoints.Catalog.ServiceOfferings;

namespace PlanelyApp.Api.Endpoints.Catalog;

/// <summary>
/// The endpoints of the Catalog module. Each resource has its own folder and its own <c>Map...</c>; this is the one line that
/// <c>Program</c> knows, so a new resource is a line here and none there.
/// </summary>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapServiceCategories();
        endpoints.MapServiceOfferings();

        return endpoints;
    }
}