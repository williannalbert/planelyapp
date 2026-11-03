using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Start;

internal sealed class StartAppointmentValidator : AbstractValidator<StartAppointmentCommand>
{
    public StartAppointmentValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}