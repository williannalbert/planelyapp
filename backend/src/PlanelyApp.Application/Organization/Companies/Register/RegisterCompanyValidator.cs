using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Companies.Register;

internal sealed class RegisterCompanyValidator : AbstractValidator<RegisterCompanyCommand>
{
    public RegisterCompanyValidator()
    {
        RuleFor(command => command.TradeName).NotEmpty();
        RuleFor(command => command.LegalName).NotEmpty();
        RuleFor(command => command.Cnpj).NotEmpty().AcceptedBy(Cnpj.Create);
        RuleFor(command => command.Email).NotEmpty().AcceptedBy(Email.Create);
        RuleFor(command => command.Phone).NotEmpty().AcceptedBy(Phone.Create);
        RuleFor(command => command.DefaultLanguage).NotEmpty().AcceptedBy(Language.Create);
    }
}