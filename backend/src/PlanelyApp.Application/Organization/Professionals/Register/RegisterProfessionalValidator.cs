using FluentValidation;

namespace PlanelyApp.Application.Organization.Professionals.Register;

internal sealed class RegisterProfessionalValidator : AbstractValidator<RegisterProfessionalCommand>
{
    public RegisterProfessionalValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
    }
}