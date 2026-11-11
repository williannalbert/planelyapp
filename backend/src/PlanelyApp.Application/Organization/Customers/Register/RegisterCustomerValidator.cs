using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.Register;

internal sealed class RegisterCustomerValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        // Everything is optional here: whether a contact is needed is a Domain rule (Customer.ContactRequired).
        RuleFor(command => command.Email).AcceptedBy(Email.Create);
        RuleFor(command => command.Phone).AcceptedBy(Phone.Create);
        RuleFor(command => command.PreferredLanguage).AcceptedBy(Language.Create);
    }
}