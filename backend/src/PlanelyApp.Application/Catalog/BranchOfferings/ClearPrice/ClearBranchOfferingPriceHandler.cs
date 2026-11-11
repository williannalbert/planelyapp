using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;

internal sealed class ClearBranchOfferingPriceHandler(IBranchRepository branches, IServiceOfferingRepository services)
    : ICommandHandler<ClearBranchOfferingPriceCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ClearBranchOfferingPriceCommand command,
        CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        var service = await services.GetByIdAsync(command.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        branch.ClearCustomPrice(service.Id);

        return Result.Success();
    }
}