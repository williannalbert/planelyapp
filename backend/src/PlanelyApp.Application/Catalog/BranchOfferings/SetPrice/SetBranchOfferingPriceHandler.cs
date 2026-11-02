using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;

internal sealed class SetBranchOfferingPriceHandler(IBranchRepository branches, IServiceOfferingRepository services)
    : ICommandHandler<SetBranchOfferingPriceCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetBranchOfferingPriceCommand command,
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

        // The Domain refuses a service on request (Branch.CustomPriceNotAllowed) and one of another company.
        branch.SetCustomPrice(service, Money.From(command.Price));

        return Result.Success();
    }
}