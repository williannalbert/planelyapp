using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Cancel;

internal sealed class CancelAppointmentValidator : AbstractValidator<CancelAppointmentCommand>
{
    // The size of the reason is the Domain's.
    public CancelAppointmentValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}