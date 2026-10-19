using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Pricing;

/// <summary>
/// Domain service: works out the price of a service in a branch (RN13) applying the best promotion (RN15).
/// Pure: it receives everything it needs, so the caller decides how the candidate promotions are loaded.
/// </summary>
public static class PriceCalculator
{
    public static PriceQuote Calculate(
        ServiceOffering service,
        Branch branch,
        DateTime appointmentStartUtc,
        IEnumerable<Promotion> promotions)
    {
        if (service.CompanyId != branch.CompanyId)
        {
            throw new DomainException("PriceCalculator.CompanyMismatch");
        }

        // Promotions and weekdays are evaluated on the day of the appointment AT THE BRANCH (RN15).
        var localDate = branch.GetLocalDate(appointmentStartUtc);

        // On request ignores any branch price or promotion left over from when the service had a fixed price.
        if (service.PricingType != PricingType.Fixed)
        {
            return PriceQuote.OnRequest();
        }

        // A fixed price service always has a base price (RN18).
        var listPrice = branch.GetCustomPrice(service.Id) ?? service.BasePrice!;

        var best = promotions
            .Where(promotion => promotion.AppliesTo(branch.Id, service.Id, localDate))
            .Select(promotion => new { Promotion = promotion, Discount = promotion.CalculateDiscount(listPrice) })
            .Where(candidate => candidate.Discount > Money.Zero)
            .OrderByDescending(candidate => candidate.Discount.Amount)
            .ThenBy(candidate => candidate.Promotion.Id)
            .FirstOrDefault();

        return best is null
            ? PriceQuote.Fixed(listPrice)
            : PriceQuote.Fixed(listPrice, best.Discount, best.Promotion.Id);
    }
}