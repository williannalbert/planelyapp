using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.RemoveService;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class RemovePromotionServiceTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task Removing_should_take_the_service_out()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut, beard);

        var result = await Handler(promotion).HandleAsync(
            new RemovePromotionServiceCommand(promotion.Id, haircut.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.ServiceIds.ShouldBe([beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_the_promotion_does_not_have_should_be_ignored()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var result = await Handler(promotion).HandleAsync(
            new RemovePromotionServiceCommand(promotion.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.ServiceIds.ShouldBe([haircut.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task The_last_service_should_not_be_removed()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion).HandleAsync(
            new RemovePromotionServiceCommand(promotion.Id, haircut.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.ServicesRequired");
        promotion.ServiceIds.ShouldBe([haircut.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_bundle_should_not_go_below_two_services()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var bundle = PromotionData.NewBundleOf(_companyId, haircut, beard);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(bundle).HandleAsync(
            new RemovePromotionServiceCommand(bundle.Id, beard.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");
        bundle.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_bundle_of_three_should_lose_one()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var wash = CatalogData.NewFixedService(_companyId, "Wash");
        var bundle = PromotionData.NewBundleOf(_companyId, haircut, beard, wash);

        var result = await Handler(bundle).HandleAsync(
            new RemovePromotionServiceCommand(bundle.Id, wash.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        bundle.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new RemovePromotionServiceCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task Missing_ids_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RemovePromotionServiceValidator(), new RemovePromotionServiceCommand(Guid.Empty, Guid.Empty));

        problems.ShouldBe(
            ["PromotionId:Validation.Required", "ServiceOfferingId:Validation.Required"], ignoreOrder: true);
    }

    private static RemovePromotionServiceHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion));
}