using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.DisableOffering;

internal sealed class DisableProfessionalOfferingHandler(IProfessionalRepository professionals)
    : ICommandHandler<DisableProfessionalOfferingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        DisableProfessionalOfferingCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // No check on the service: one that no longer exists must still be removable from the professional.
        professional.DisableOffering(command.ServiceOfferingId);

        return Result.Success();
    }
}