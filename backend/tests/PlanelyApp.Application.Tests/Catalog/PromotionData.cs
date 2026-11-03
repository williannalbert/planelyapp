using NSubstitute;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Tests.Catalog;

internal static class PromotionData
{
    public static readonly DateOnly Start = new(2027, 11, 20);
    public static readonly DateOnly End = new(2027, 11, 30);

    public static ValidityPeriod November => ValidityPeriod.Create(Start, End);

    public static Promotion NewPromotion(Guid? companyId = null)
    {
        var company = companyId ?? Guid.NewGuid();

        return Promotion.CreateForAllBranches(
            company, "Black Friday", Discount.Percentage(10m), November, [CatalogData.NewFixedService(company)]);
    }

    /// <summary>A repository that finds this promotion (or none) whatever the id.</summary>
    public static IPromotionRepository PromotionsWith(Promotion? promotion)
    {
        var promotions = Substitute.For<IPromotionRepository>();
        promotions.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(promotion);

        return promotions;
    }

    /// <summary>A repository that answers with these services, in this order, whatever the ids.</summary>
    public static IServiceOfferingRepository ServicesListing(params ServiceOffering[] found)
    {
        var services = Substitute.For<IServiceOfferingRepository>();
        services.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(found);

        return services;
    }

    /// <summary>A per-service promotion for all branches with these services.</summary>
    public static Promotion NewPromotionOf(Guid companyId, params ServiceOffering[] services) =>
        Promotion.CreateForAllBranches(companyId, "Black Friday", Discount.Percentage(10m), November, services);

    /// <summary>A bundle for all branches with these services.</summary>
    public static Promotion NewBundleOf(Guid companyId, params ServiceOffering[] services) =>
        Promotion.CreateBundleForAllBranches(companyId, "Combo", Discount.Percentage(10m), November, services);

    /// <summary>A per-service promotion for only these branches.</summary>
    public static Promotion NewPromotionForBranches(Guid companyId, params Guid[] branchIds) =>
        Promotion.CreateForBranches(
            companyId,
            "Black Friday",
            Discount.Percentage(10m),
            November,
            [CatalogData.NewFixedService(companyId)],
            branchIds);
}