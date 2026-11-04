using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Remind;

internal sealed class SendAppointmentReminderValidator : AbstractValidator<SendAppointmentReminderCommand>
{
    public SendAppointmentReminderValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}