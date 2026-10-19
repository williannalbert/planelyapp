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

    /// <summary>
    /// Prices all the services of one appointment together, in the same order they were given (RN13, RN15).
    /// Bundles depend on the set of services, so they can only be evaluated here. A service gets at most one
    /// promotion: the calculator picks the combination of bundles and per-service promotions with the largest
    /// total discount. On a tie, the bundle with the lowest Id wins.
    /// </summary>
    public static IReadOnlyList<PriceQuote> CalculateAll(
        IEnumerable<ServiceOffering> services,
        Branch branch,
        DateTime appointmentStartUtc,
        IEnumerable<Promotion> promotions)
    {
        var serviceList = services.ToList();
        var promotionList = promotions.ToList();

        // What each service gets on its own: branch price (RN13) and best per-service promotion (RN15).
        var alone = serviceList
            .Select(service => Calculate(service, branch, appointmentStartUtc, promotionList))
            .ToList();

        var localDate = branch.GetLocalDate(appointmentStartUtc);
        var offers = BuildBundleOffers(serviceList, alone, branch, localDate, promotionList);
        var chosen = ChooseBestOffers(offers, alone);

        var quotes = alone.ToList();

        foreach (var offer in chosen)
        {
            for (var position = 0; position < offer.Members.Count; position++)
            {
                var index = offer.Members[position];
                quotes[index] = PriceQuote.FromBundle(alone[index].ListPrice!, offer.Shares[position], offer.Promotion.Id);
            }
        }

        return quotes;
    }

    private static List<BundleOffer> BuildBundleOffers(
        IReadOnlyList<ServiceOffering> services,
        IReadOnlyList<PriceQuote> alone,
        Branch branch,
        DateOnly localDate,
        IEnumerable<Promotion> promotions)
    {
        // Only fixed price services can be part of a bundle. When a service appears twice,
        // the first occurrence is the one that counts for the bundle.
        var firstIndexByServiceId = new Dictionary<Guid, int>();

        for (var index = 0; index < services.Count; index++)
        {
            if (!alone[index].IsOnRequest)
            {
                firstIndexByServiceId.TryAdd(services[index].Id, index);
            }
        }

        var offers = new List<BundleOffer>();

        foreach (var promotion in promotions.OrderBy(promotion => promotion.Id))
        {
            if (!promotion.AppliesToBundle(branch.Id, firstIndexByServiceId.Keys, localDate))
            {
                continue;
            }

            var members = promotion.ServiceIds.Select(serviceId => firstIndexByServiceId[serviceId]).ToList();
            var prices = members.Select(index => alone[index].ListPrice!).ToList();
            var total = prices.Aggregate(Money.Zero, (sum, price) => sum.Add(price));
            var discount = promotion.CalculateDiscount(total);

            if (discount <= Money.Zero)
            {
                continue;
            }

            offers.Add(new BundleOffer(promotion, members, SplitDiscount(discount, prices)));
        }

        return offers;
    }

    /// <summary>
    /// Picks the set of bundles (no service in two of them) that adds the most discount compared with
    /// letting each service use its own best promotion. Tries every valid set: there are only a few bundles
    /// that can apply to the same appointment, and a greedy choice would give worse prices.
    /// </summary>
    private static List<BundleOffer> ChooseBestOffers(
        IReadOnlyList<BundleOffer> offers,
        IReadOnlyList<PriceQuote> alone)
    {
        // The gain of a bundle is what it gives beyond what its services already got on their own.
        var candidates = offers
            .Select(offer => (Offer: offer, Gain: offer.Discount.Amount - offer.Members.Sum(index => alone[index].Discount.Amount)))
            .Where(candidate => candidate.Gain > 0)
            .ToList();

        var bestGain = 0m;
        var bestChoice = new List<BundleOffer>();
        var current = new List<BundleOffer>();
        var covered = new HashSet<int>();

        void Search(int next, decimal gain)
        {
            if (gain > bestGain)
            {
                bestGain = gain;
                bestChoice = [.. current];
            }

            for (var position = next; position < candidates.Count; position++)
            {
                var (offer, offerGain) = candidates[position];

                if (offer.Members.Any(covered.Contains))
                {
                    continue;
                }

                current.Add(offer);
                covered.UnionWith(offer.Members);

                Search(position + 1, gain + offerGain);

                covered.ExceptWith(offer.Members);
                current.RemoveAt(current.Count - 1);
            }
        }

        Search(0, 0m);

        return bestChoice;
    }

    /// <summary>
    /// Splits the bundle discount among the services in proportion to their prices, in whole cents.
    /// The cents left over by rounding down go to the largest remainders, so the shares add up exactly.
    /// </summary>
    private static List<Money> SplitDiscount(Money discount, IReadOnlyList<Money> prices)
    {
        var totalCents = prices.Sum(price => ToCents(price));
        var discountCents = ToCents(discount);

        var shares = new long[prices.Count];
        var remainders = new long[prices.Count];

        for (var index = 0; index < prices.Count; index++)
        {
            var numerator = checked(discountCents * ToCents(prices[index]));
            shares[index] = numerator / totalCents;
            remainders[index] = numerator % totalCents;
        }

        var leftover = (int)(discountCents - shares.Sum());

        var receivers = Enumerable.Range(0, prices.Count)
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => index)
            .Take(leftover);

        foreach (var receiver in receivers)
        {
            shares[receiver]++;
        }

        return shares.Select(cents => Money.From(cents / 100m)).ToList();
    }

    private static long ToCents(Money money) => (long)(money.Amount * 100m);

    private sealed record BundleOffer(Promotion Promotion, IReadOnlyList<int> Members, IReadOnlyList<Money> Shares)
    {
        public Money Discount { get; } = Shares.Aggregate(Money.Zero, (sum, share) => sum.Add(share));
    }
}