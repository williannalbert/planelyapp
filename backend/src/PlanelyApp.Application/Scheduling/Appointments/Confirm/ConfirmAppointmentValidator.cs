using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Confirm;

internal sealed class ConfirmAppointmentValidator : AbstractValidator<ConfirmAppointmentCommand>
{
    public ConfirmAppointmentValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}