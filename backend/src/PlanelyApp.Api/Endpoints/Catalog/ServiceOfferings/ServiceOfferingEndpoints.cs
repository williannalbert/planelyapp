using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Activate;
using PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Catalog.ServiceOfferings.Get;
using PlanelyApp.Application.Catalog.ServiceOfferings.List;
using PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Catalog.ServiceOfferings;

internal static class ServiceOfferingEndpoints
{
    // The whole staff offers and books services, so it reads the catalog; the platform administrator has no company to work in.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    public static IEndpointRouteBuilder MapServiceOfferings(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints
            .MapGroup("/companies/{companyId:guid}/service-offerings")
            .WithTags("Service offerings");

        services
            .MapGet("/", async (
                string? search,
                Guid? categoryId,
                bool? isActive,
                int? page,
                int? pageSize,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(
                    new ListServiceOfferingsQuery(search, categoryId, isActive, page ?? 1, pageSize ?? PageLimits.DefaultPageSize),
                    cancellationToken)).ToHttp())
            .WithName("ListServiceOfferings")
            .WithSummary("One page of the services, with an optional search by name and filters of category and active or inactive.")
            .RequireRoles(CompanyRoles);

        services
            .MapPost("/", async (
                Guid companyId, CreateServiceOfferingCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(id => $"/companies/{companyId}/service-offerings/{id}"))
            .WithName("CreateServiceOffering")
            .WithSummary("Creates a service, with a fixed price or on request. The name is unique in the company.")
            .RequireRoles(UserRole.CompanyAdmin);

        var service = services.MapGroup("/{serviceOfferingId:guid}");

        service
            .MapGet("/", async (Guid serviceOfferingId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetServiceOfferingQuery(serviceOfferingId), cancellationToken)).ToHttp())
            .WithName("GetServiceOffering")
            .WithSummary("Reads a service.")
            .RequireRoles(CompanyRoles);

        // Every id of a command is the one of the route: a body that could bring its own would go around what the route says.
        service
            .MapPut("/", async (
                Guid serviceOfferingId,
                ServiceOfferingDetailsBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateServiceOfferingDetailsCommand(
                        serviceOfferingId, body.Name, body.DurationMinutes, body.Description, body.CategoryId),
                    cancellationToken)).ToNoContent())
            .WithName("UpdateServiceOffering")
            .WithSummary("Replaces the name, the duration, the description and the category of a service.")
            .RequireRoles(UserRole.CompanyAdmin);

        service
            .MapPut("/pricing", async (
                Guid serviceOfferingId, PricingData pricing, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new ChangeServiceOfferingPricingCommand(serviceOfferingId, pricing), cancellationToken)).ToNoContent())
            .WithName("ChangeServiceOfferingPricing")
            .WithSummary("Makes the service fixed price (with a base price) or on request.")
            .RequireRoles(UserRole.CompanyAdmin);

        service
            .MapPost("/activate", async (Guid serviceOfferingId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new ActivateServiceOfferingCommand(serviceOfferingId), cancellationToken)).ToNoContent())
            .WithName("ActivateServiceOffering")
            .WithSummary("Activates a service.")
            .RequireRoles(UserRole.CompanyAdmin);

        service
            .MapPost("/deactivate", async (Guid serviceOfferingId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new DeactivateServiceOfferingCommand(serviceOfferingId), cancellationToken)).ToNoContent())
            .WithName("DeactivateServiceOffering")
            .WithSummary("Deactivates a service. Nothing is deleted: professionals and promotions that cite it are not touched.")
            .RequireRoles(UserRole.CompanyAdmin);

        return endpoints;
    }
}