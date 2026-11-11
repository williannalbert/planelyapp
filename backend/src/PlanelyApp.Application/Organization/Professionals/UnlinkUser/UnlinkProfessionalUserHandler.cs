using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.UnlinkUser;

internal sealed class UnlinkProfessionalUserHandler(IProfessionalRepository professionals)
    : ICommandHandler<UnlinkProfessionalUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UnlinkProfessionalUserCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        professional.UnlinkUser();

        return Result.Success();
    }
}