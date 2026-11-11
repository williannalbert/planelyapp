using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Complete;

internal sealed class CompleteAppointmentValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}