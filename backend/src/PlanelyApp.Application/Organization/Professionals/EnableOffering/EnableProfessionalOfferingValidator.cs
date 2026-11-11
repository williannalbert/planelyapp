using FluentValidation;

namespace PlanelyApp.Application.Organization.Professionals.EnableOffering;

internal sealed class EnableProfessionalOfferingValidator : AbstractValidator<EnableProfessionalOfferingCommand>
{
    public EnableProfessionalOfferingValidator()
    {
        // The range of the duration is the Domain's (ProfessionalOffering.Create).
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
    }
}