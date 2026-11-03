using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.MarkNoShow;

internal sealed class MarkAppointmentAsNoShowValidator : AbstractValidator<MarkAppointmentAsNoShowCommand>
{
    public MarkAppointmentAsNoShowValidator() => RuleFor(command => command.AppointmentId).NotEmpty();
}