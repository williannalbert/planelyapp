using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.UpdateProfile;

internal sealed class UpdateCustomerProfileValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileValidator()
    {
        RuleFor(command => command.Cpf).AcceptedBy(Cpf.Create);
        RuleFor(command => command.PreferredLanguage).AcceptedBy(Language.Create);
    }
}