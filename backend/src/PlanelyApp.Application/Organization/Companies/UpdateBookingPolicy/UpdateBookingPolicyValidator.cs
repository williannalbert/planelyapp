using FluentValidation;

namespace PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;

internal sealed class UpdateBookingPolicyValidator : AbstractValidator<UpdateBookingPolicyCommand>
{
    public UpdateBookingPolicyValidator()
    {
        // The numbers are checked by the Domain (BookingPolicy.Create), with the code of each one.
        RuleFor(command => command.Policy).NotNull();
    }
}