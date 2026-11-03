using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.MarkNoShow;

internal sealed class MarkAppointmentAsNoShowHandler(
    AppointmentAccess access,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<MarkAppointmentAsNoShowCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        MarkAppointmentAsNoShowCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForStaffAsync(command.AppointmentId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        // Before the start time it makes no sense: the Domain refuses it.
        loaded.Value.MarkAsNoShow(currentUser.Id, timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}