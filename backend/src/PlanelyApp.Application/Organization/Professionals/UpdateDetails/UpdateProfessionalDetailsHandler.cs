using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.UpdateDetails;

internal sealed class UpdateProfessionalDetailsHandler(IProfessionalRepository professionals)
    : ICommandHandler<UpdateProfessionalDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateProfessionalDetailsCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        professional.UpdateDetails(command.Name, command.Specialty);

        return Result.Success();
    }
}