using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.RemoveService;

internal sealed class RemovePromotionServiceValidator : AbstractValidator<RemovePromotionServiceCommand>
{
    public RemovePromotionServiceValidator()
    {
        RuleFor(command => command.PromotionId).NotEmpty();
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
    }
}