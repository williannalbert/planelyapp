using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Scheduling.Pricing;

namespace PlanelyApp.Application.Catalog.Pricing.GetPrice;

internal sealed class GetPriceHandler(
    IBranchRepository branches,
    IServiceOfferingRepository services,
    IPromotionRepository promotions)
    : IQueryHandler<GetPriceQuery, PriceQuoteDto>
{
    public async Task<Result<PriceQuoteDto>> HandleAsync(GetPriceQuery query, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(query.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        // A missing id and an id of another company look the same: the repository hides the other companies.
        var distinctIds = query.ServiceIds.Distinct().ToList();
        var found = (await services.ListByIdsAsync(distinctIds, cancellationToken)).ToDictionary(service => service.Id);

        if (found.Count != distinctIds.Count)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        // The promotions are judged on the day AT THE BRANCH (RN15), not on the UTC day. The Domain refuses a time
        // that is not UTC.
        var localDate = branch.GetLocalDate(query.StartUtc);
        var inForce = await promotions.ListInForceAsync(localDate, cancellationToken);

        var asked = query.ServiceIds.Select(id => found[id]).ToList();
        var quotes = PriceCalculator.CalculateAll(asked, branch, query.StartUtc, inForce);
        var promotionNames = inForce.ToDictionary(promotion => promotion.Id, promotion => promotion.Name);

        var lines = asked
            .Zip(quotes, (service, quote) => new PriceLineDto(
                service.Id,
                service.Name,
                quote.IsOnRequest,
                quote.ListPrice?.Amount,
                quote.Discount.Amount,
                quote.FinalPrice?.Amount,
                quote.PromotionId,
                quote.PromotionId is { } promotionId && promotionNames.TryGetValue(promotionId, out var promotionName)
                    ? promotionName
                    : null,
                quote.IsFromBundle,
                service.IsActive && branch.Offers(service.Id)))
            .ToList();

        return new PriceQuoteDto(
            lines,
            lines.Sum(line => line.ListPrice ?? 0m),
            lines.Sum(line => line.Discount),
            lines.Sum(line => line.FinalPrice ?? 0m),
            lines.Any(line => line.IsOnRequest));
    }
}