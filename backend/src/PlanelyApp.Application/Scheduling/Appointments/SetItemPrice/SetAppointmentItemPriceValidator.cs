using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;

internal sealed class SetAppointmentItemPriceValidator : AbstractValidator<SetAppointmentItemPriceCommand>
{
    // A negative price and the range of the duration are the Domain's.
    public SetAppointmentItemPriceValidator()
    {
        RuleFor(command => command.AppointmentId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Price).PrecisionScale(12, 2, true);
    }
}