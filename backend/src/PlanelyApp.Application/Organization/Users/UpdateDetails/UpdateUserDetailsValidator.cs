using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Users.UpdateDetails;

internal sealed class UpdateUserDetailsValidator : AbstractValidator<UpdateUserDetailsCommand>
{
    public UpdateUserDetailsValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().AcceptedBy(Email.Create);
        RuleFor(command => command.PreferredLanguage).AcceptedBy(Language.Create);
    }
}