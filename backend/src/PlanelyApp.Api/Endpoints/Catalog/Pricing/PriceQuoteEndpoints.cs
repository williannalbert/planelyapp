using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Catalog.Pricing.GetPrice;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Catalog.Pricing;

internal static class PriceQuoteEndpoints
{
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    public static IEndpointRouteBuilder MapPriceQuotes(this IEndpointRouteBuilder endpoints)
    {
        var quotes = endpoints
            .MapGroup("/companies/{companyId:guid}/branches/{branchId:guid}/price-quotes")
            .WithTags("Price quotes");

        // A POST because the list of services is a body, but it creates nothing and can be repeated: 200, not 201.
        quotes
            .MapPost("/", async (
                Guid branchId, PriceQuoteBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(
                    new GetPriceQuery(branchId, body.StartUtc, body.ServiceIds ?? []), cancellationToken)).ToHttp())
            .WithName("QuotePrice")
            .WithSummary("The price of the services of an appointment at a branch, with the promotions of that moment.")
            .RequireRoles(CompanyRoles);

        return endpoints;
    }
}