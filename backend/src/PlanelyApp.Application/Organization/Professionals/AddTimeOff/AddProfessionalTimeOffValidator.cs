using FluentValidation;

namespace PlanelyApp.Application.Organization.Professionals.AddTimeOff;

internal sealed class AddProfessionalTimeOffValidator : AbstractValidator<AddProfessionalTimeOffCommand>
{
    public AddProfessionalTimeOffValidator()
    {
        // A date that was not sent arrives as the default value. The order and the UTC kind are the Domain's rules.
        RuleFor(command => command.StartUtc).NotEmpty();
        RuleFor(command => command.EndUtc).NotEmpty();
    }
}