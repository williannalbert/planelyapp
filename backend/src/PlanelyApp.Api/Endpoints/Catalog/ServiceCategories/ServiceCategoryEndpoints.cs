using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceCategories.List;
using PlanelyApp.Application.Catalog.ServiceCategories.Rename;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Catalog.ServiceCategories;

internal static class ServiceCategoryEndpoints
{
    // The whole staff offers and books services, so it reads the catalog; the platform administrator has no company to work in.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    public static IEndpointRouteBuilder MapServiceCategories(this IEndpointRouteBuilder endpoints)
    {
        var categories = endpoints
            .MapGroup("/companies/{companyId:guid}/service-categories")
            .WithTags("Service categories");

        categories
            .MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new ListServiceCategoriesQuery(), cancellationToken)).ToHttp())
            .WithName("ListServiceCategories")
            .WithSummary("Lists the categories of the company, ordered by name.")
            .RequireRoles(CompanyRoles);

        // A category is read inside the list (there is no read of a single one), so the Location is the list.
        categories
            .MapPost("/", async (
                Guid companyId, CreateServiceCategoryCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(_ => $"/companies/{companyId}/service-categories"))
            .WithName("CreateServiceCategory")
            .WithSummary("Creates a category. The name is unique in the company.")
            .RequireRoles(UserRole.CompanyAdmin);

        categories
            .MapPut("/{serviceCategoryId:guid}", async (
                Guid serviceCategoryId, ServiceCategoryBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new RenameServiceCategoryCommand(serviceCategoryId, body.Name), cancellationToken)).ToNoContent())
            .WithName("RenameServiceCategory")
            .WithSummary("Renames a category.")
            .RequireRoles(UserRole.CompanyAdmin);

        return endpoints;
    }
}