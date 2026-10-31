using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.Deactivate;

internal sealed class DeactivateProfessionalHandler(IProfessionalRepository professionals)
    : ICommandHandler<DeactivateProfessionalCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        DeactivateProfessionalCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        professional.Deactivate();

        return Result.Success();
    }
}