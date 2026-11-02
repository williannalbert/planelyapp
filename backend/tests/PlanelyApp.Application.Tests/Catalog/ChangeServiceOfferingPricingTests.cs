using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ChangeServiceOfferingPricingTests
{
    [Fact]
    public async Task A_fixed_price_service_can_change_its_price()
    {
        var service = CatalogData.NewFixedService();

        var result = await Handler(service).HandleAsync(
            new ChangeServiceOfferingPricingCommand(service.Id, new PricingData(PricingType.Fixed, 80m)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.BasePrice!.Amount.ShouldBe(80m);
    }

    [Fact]
    public async Task A_fixed_price_service_can_become_on_request_and_lose_its_price()
    {
        var service = CatalogData.NewFixedService();

        var result = await Handler(service).HandleAsync(
            new ChangeServiceOfferingPricingCommand(service.Id, new PricingData(PricingType.OnRequest)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.PricingType.ShouldBe(PricingType.OnRequest);
        service.BasePrice.ShouldBeNull();
    }

    [Fact]
    public async Task An_on_request_service_can_become_fixed_price()
    {
        var service = CatalogData.NewOnRequestService();

        var result = await Handler(service).HandleAsync(
            new ChangeServiceOfferingPricingCommand(service.Id, new PricingData(PricingType.Fixed, 300m)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.PricingType.ShouldBe(PricingType.Fixed);
        service.BasePrice!.Amount.ShouldBe(300m);
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new ChangeServiceOfferingPricingCommand(Guid.NewGuid(), new PricingData(PricingType.OnRequest)),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task A_negative_price_should_be_refused_by_the_domain_and_leave_the_service_untouched()
    {
        var service = CatalogData.NewFixedService();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(service).HandleAsync(
            new ChangeServiceOfferingPricingCommand(service.Id, new PricingData(PricingType.Fixed, -5m)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Money.NegativeAmount");
        service.BasePrice!.Amount.ShouldBe(50m);
    }

    [Fact]
    public async Task A_missing_id_and_a_fixed_price_without_a_price_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ChangeServiceOfferingPricingValidator(),
            new ChangeServiceOfferingPricingCommand(Guid.Empty, new PricingData(PricingType.Fixed)));

        problems.ShouldBe(
            ["ServiceOfferingId:Validation.Required", "Pricing.BasePrice:Validation.Required"], ignoreOrder: true);
    }

    private static ChangeServiceOfferingPricingHandler Handler(ServiceOffering? service) =>
        new(CatalogData.ServicesWith(service));
}