using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Reschedule;

internal sealed class RescheduleAppointmentValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    // That the time is UTC, the notice and the window are the Domain's.
    public RescheduleAppointmentValidator()
    {
        RuleFor(command => command.AppointmentId).NotEmpty();
        RuleFor(command => command.NewStartUtc).NotEmpty();
    }
}