using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.IncludeServices;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class IncludePromotionServicesTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task Including_should_add_the_services_at_the_end()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var result = await Handler(promotion, beard).HandleAsync(
            new IncludePromotionServicesCommand(promotion.Id, [beard.Id]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_the_promotion_already_has_should_be_ignored()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var result = await Handler(promotion, haircut, beard).HandleAsync(
            new IncludePromotionServicesCommand(promotion.Id, [haircut.Id, beard.Id, beard.Id]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var handler = new IncludePromotionServicesHandler(
            PromotionData.PromotionsWith(null), PromotionData.ServicesListing());

        var result = await handler.HandleAsync(
            new IncludePromotionServicesCommand(Guid.NewGuid(), [Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_not_found_and_leave_the_promotion_alone()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var result = await Handler(promotion, beard).HandleAsync(
            new IncludePromotionServicesCommand(promotion.Id, [beard.Id, Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        promotion.ServiceIds.ShouldBe([haircut.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_on_request_should_be_refused_by_the_domain_and_leave_the_promotion_alone()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var tattoo = CatalogData.NewOnRequestService(_companyId);
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(promotion, beard, tattoo).HandleAsync(
            new IncludePromotionServicesCommand(promotion.Id, [beard.Id, tattoo.Id]),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");
        promotion.ServiceIds.ShouldBe([haircut.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new IncludePromotionServicesValidator(),
            new IncludePromotionServicesCommand(Guid.NewGuid(), [Guid.NewGuid()]));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_list_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new IncludePromotionServicesValidator(), new IncludePromotionServicesCommand(Guid.NewGuid(), []));

        problems.ShouldBe(["ServiceIds:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_service_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new IncludePromotionServicesValidator(),
            new IncludePromotionServicesCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.Empty]));

        problems.ShouldBe(["ServiceIds[1]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new IncludePromotionServicesValidator(),
            new IncludePromotionServicesCommand(Guid.Empty, [Guid.NewGuid()]));

        problems.ShouldBe(["PromotionId:Validation.Required"], ignoreOrder: true);
    }

    private static IncludePromotionServicesHandler Handler(Promotion promotion, params ServiceOffering[] stored) =>
        new(PromotionData.PromotionsWith(promotion), PromotionData.ServicesListing(stored));
}