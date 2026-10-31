using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.Activate;

internal sealed class ActivateProfessionalHandler(IProfessionalRepository professionals)
    : ICommandHandler<ActivateProfessionalCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ActivateProfessionalCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        professional.Activate();

        return Result.Success();
    }
}