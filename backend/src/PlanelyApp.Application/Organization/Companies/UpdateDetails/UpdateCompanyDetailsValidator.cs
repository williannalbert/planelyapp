using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Companies.UpdateDetails;

internal sealed class UpdateCompanyDetailsValidator : AbstractValidator<UpdateCompanyDetailsCommand>
{
    public UpdateCompanyDetailsValidator()
    {
        RuleFor(command => command.TradeName).NotEmpty();
        RuleFor(command => command.LegalName).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().AcceptedBy(Email.Create);
        RuleFor(command => command.Phone).NotEmpty().AcceptedBy(Phone.Create);
        RuleFor(command => command.DefaultLanguage).NotEmpty().AcceptedBy(Language.Create);
    }
}