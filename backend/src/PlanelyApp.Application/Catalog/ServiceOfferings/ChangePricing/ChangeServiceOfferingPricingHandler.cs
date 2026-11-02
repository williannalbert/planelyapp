using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;

internal sealed class ChangeServiceOfferingPricingHandler(IServiceOfferingRepository services)
    : ICommandHandler<ChangeServiceOfferingPricingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ChangeServiceOfferingPricingCommand command,
        CancellationToken cancellationToken)
    {
        var service = await services.GetByIdAsync(command.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        if (command.Pricing.Type == PricingType.Fixed)
        {
            // The validator guarantees a base price for a fixed price service.
            service.SetFixedPrice(Money.From(command.Pricing.BasePrice!.Value));
        }
        else
        {
            service.SetOnRequest();
        }

        return Result.Success();
    }
}