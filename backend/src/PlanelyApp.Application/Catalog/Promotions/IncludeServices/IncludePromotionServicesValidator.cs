using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.IncludeServices;

internal sealed class IncludePromotionServicesValidator : AbstractValidator<IncludePromotionServicesCommand>
{
    public IncludePromotionServicesValidator()
    {
        RuleFor(command => command.PromotionId).NotEmpty();
        RuleFor(command => command.ServiceIds).NotEmpty();
        RuleForEach(command => command.ServiceIds).NotEqual(Guid.Empty);
    }
}