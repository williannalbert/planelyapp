using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings.Activate;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ServiceOfferingStatusTests
{
    [Fact]
    public async Task Deactivating_should_turn_the_service_off()
    {
        var service = CatalogData.NewFixedService();
        var handler = new DeactivateServiceOfferingHandler(CatalogData.ServicesWith(service));

        var result = await handler.HandleAsync(
            new DeactivateServiceOfferingCommand(service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activating_should_turn_the_service_back_on()
    {
        var service = CatalogData.NewFixedService();
        service.Deactivate();
        var handler = new ActivateServiceOfferingHandler(CatalogData.ServicesWith(service));

        var result = await handler.HandleAsync(
            new ActivateServiceOfferingCommand(service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivating_a_missing_service_should_be_not_found()
    {
        var handler = new DeactivateServiceOfferingHandler(CatalogData.ServicesWith(null));

        var result = await handler.HandleAsync(
            new DeactivateServiceOfferingCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task Activating_a_missing_service_should_be_not_found()
    {
        var handler = new ActivateServiceOfferingHandler(CatalogData.ServicesWith(null));

        var result = await handler.HandleAsync(
            new ActivateServiceOfferingCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}