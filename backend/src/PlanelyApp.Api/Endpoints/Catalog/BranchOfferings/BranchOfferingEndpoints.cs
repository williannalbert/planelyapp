using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;
using PlanelyApp.Application.Catalog.BranchOfferings.List;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Catalog.BranchOfferings;

internal static class BranchOfferingEndpoints
{
    // The staff reads what each branch offers; changing it is for the administrator. The platform administrator has no branch.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    public static IEndpointRouteBuilder MapBranchOfferings(this IEndpointRouteBuilder endpoints)
    {
        var offerings = endpoints
            .MapGroup("/companies/{companyId:guid}/branches/{branchId:guid}/service-offerings")
            .WithTags("Branch service offerings");

        offerings
            .MapGet("/", async (
                Guid branchId,
                string? search,
                Guid? categoryId,
                bool? isAvailable,
                int? page,
                int? pageSize,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(
                    new ListBranchOfferingsQuery(
                        branchId, search, categoryId, isAvailable, page ?? 1, pageSize ?? PageLimits.DefaultPageSize),
                    cancellationToken)).ToHttp())
            .WithName("ListBranchOfferings")
            .WithSummary("The services of a branch, with the base price, the price of the branch and whether each is available there.")
            .RequireRoles(CompanyRoles);

        var offering = offerings.MapGroup("/{serviceOfferingId:guid}");

        offering
            .MapPut("/availability", async Task<IResult> (
                Guid branchId,
                Guid serviceOfferingId,
                AvailabilityBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                // Left out, it would arrive as false, and the service would leave the branch without anybody saying so.
                if (body.IsAvailable is not { } isAvailable)
                {
                    return FieldProblems.Required(nameof(SetBranchOfferingAvailabilityCommand.IsAvailable));
                }

                return (await dispatcher.SendAsync(
                    new SetBranchOfferingAvailabilityCommand(branchId, serviceOfferingId, isAvailable),
                    cancellationToken)).ToNoContent();
            })
            .WithName("SetBranchOfferingAvailability")
            .WithSummary("Makes a service available or unavailable in the branch.")
            .RequireRoles(UserRole.CompanyAdmin);

        offering
            .MapPut("/price", async Task<IResult> (
                Guid branchId,
                Guid serviceOfferingId,
                PriceBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                // Left out, it would arrive as 0, and the service would be free in the branch.
                if (body.Price is not { } price)
                {
                    return FieldProblems.Required(nameof(SetBranchOfferingPriceCommand.Price));
                }

                return (await dispatcher.SendAsync(
                    new SetBranchOfferingPriceCommand(branchId, serviceOfferingId, price), cancellationToken)).ToNoContent();
            })
            .WithName("SetBranchOfferingPrice")
            .WithSummary("Sets the price of a service in the branch. A service on request has none.")
            .RequireRoles(UserRole.CompanyAdmin);

        offering
            .MapDelete("/price", async (
                Guid branchId, Guid serviceOfferingId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new ClearBranchOfferingPriceCommand(branchId, serviceOfferingId), cancellationToken)).ToNoContent())
            .WithName("ClearBranchOfferingPrice")
            .WithSummary("Removes the price of the branch: the service goes back to the base price.")
            .RequireRoles(UserRole.CompanyAdmin);

        return endpoints;
    }
}