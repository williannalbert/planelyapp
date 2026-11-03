using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.Start;

internal sealed class StartAppointmentHandler(
    AppointmentAccess access,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<StartAppointmentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(StartAppointmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await access.LoadForStaffAsync(command.AppointmentId, cancellationToken);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        loaded.Value.Start(currentUser.Id, timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}