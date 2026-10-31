using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Professionals.AddTimeOff;

internal sealed class AddProfessionalTimeOffHandler(IProfessionalRepository professionals)
    : ICommandHandler<AddProfessionalTimeOffCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        AddProfessionalTimeOffCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The range refuses instants that are not UTC and an end before the start.
        var timeOff = professional.AddTimeOff(DateTimeRange.Create(command.StartUtc, command.EndUtc), command.Reason);

        return timeOff.Id;
    }
}