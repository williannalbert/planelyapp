using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings.Get;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class GetServiceOfferingTests
{
    [Fact]
    public async Task A_fixed_price_service_should_come_back_as_a_dto()
    {
        var categoryId = Guid.NewGuid();
        var service = CatalogData.NewFixedService();
        service.UpdateDetails("Haircut", "Scissors", TimeSpan.FromMinutes(45), categoryId);
        var handler = new GetServiceOfferingHandler(CatalogData.ServicesWith(service));

        var result = await handler.HandleAsync(
            new GetServiceOfferingQuery(service.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(service.Id);
        dto.Name.ShouldBe("Haircut");
        dto.Description.ShouldBe("Scissors");
        dto.DurationMinutes.ShouldBe(45);
        dto.PricingType.ShouldBe(PricingType.Fixed);
        dto.BasePrice.ShouldBe(50m);
        dto.CategoryId.ShouldBe(categoryId);
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_on_request_service_should_come_back_without_a_price()
    {
        var service = CatalogData.NewOnRequestService();
        var handler = new GetServiceOfferingHandler(CatalogData.ServicesWith(service));

        var result = await handler.HandleAsync(
            new GetServiceOfferingQuery(service.Id), TestContext.Current.CancellationToken);

        result.Value.PricingType.ShouldBe(PricingType.OnRequest);
        result.Value.BasePrice.ShouldBeNull();
        result.Value.DurationMinutes.ShouldBe(120);
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found()
    {
        var handler = new GetServiceOfferingHandler(CatalogData.ServicesWith(null));

        var result = await handler.HandleAsync(
            new GetServiceOfferingQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }
}