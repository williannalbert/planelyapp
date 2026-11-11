using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.Create;

internal sealed class CreatePromotionValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionValidator()
    {
        // Limits, the minimum of services, the discount range and the period are the Domain's.
        RuleFor(command => command.Kind).IsInEnum();
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Discount).NotNull().SetValidator(new DiscountDataValidator());
        RuleFor(command => command.ServiceIds).NotNull();
        RuleForEach(command => command.ServiceIds).NotEqual(Guid.Empty);

        // "All branches" and a list of branches contradict each other: that is a malformed request, not a business rule.
        RuleFor(command => command.BranchIds)
            .Must(ids => ids is null or { Count: 0 })
            .When(command => command.AppliesToAllBranches);
        RuleFor(command => command.BranchIds).NotEmpty().When(command => !command.AppliesToAllBranches);
        RuleForEach(command => command.BranchIds!).NotEqual(Guid.Empty); // a null list has nothing to iterate
    }
}