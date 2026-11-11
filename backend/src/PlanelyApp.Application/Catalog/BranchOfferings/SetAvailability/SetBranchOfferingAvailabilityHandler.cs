using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;

internal sealed class SetBranchOfferingAvailabilityHandler(
    IBranchRepository branches,
    IServiceOfferingRepository services)
    : ICommandHandler<SetBranchOfferingAvailabilityCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetBranchOfferingAvailabilityCommand command,
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

        if (command.IsAvailable)
        {
            branch.MakeOfferingAvailable(service.Id);
        }
        else
        {
            branch.MakeOfferingUnavailable(service.Id);
        }

        return Result.Success();
    }
}