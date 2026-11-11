using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.UpdateContact;

internal sealed class UpdateCustomerContactValidator : AbstractValidator<UpdateCustomerContactCommand>
{
    public UpdateCustomerContactValidator()
    {
        RuleFor(command => command.Email).AcceptedBy(Email.Create);
        RuleFor(command => command.Phone).AcceptedBy(Phone.Create);
    }
}