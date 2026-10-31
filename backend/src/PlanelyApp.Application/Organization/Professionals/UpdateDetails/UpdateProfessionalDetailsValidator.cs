using FluentValidation;

namespace PlanelyApp.Application.Organization.Professionals.UpdateDetails;

internal sealed class UpdateProfessionalDetailsValidator : AbstractValidator<UpdateProfessionalDetailsCommand>
{
    public UpdateProfessionalDetailsValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
    }
}