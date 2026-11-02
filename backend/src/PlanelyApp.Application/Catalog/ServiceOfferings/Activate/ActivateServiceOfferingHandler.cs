using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Activate;

internal sealed class ActivateServiceOfferingHandler(IServiceOfferingRepository services)
    : ICommandHandler<ActivateServiceOfferingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ActivateServiceOfferingCommand command,
        CancellationToken cancellationToken)
    {
        var service = await services.GetByIdAsync(command.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        service.Activate();

        return Result.Success();
    }
}