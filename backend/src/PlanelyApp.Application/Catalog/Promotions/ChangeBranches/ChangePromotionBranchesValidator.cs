using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeBranches;

internal sealed class ChangePromotionBranchesValidator : AbstractValidator<ChangePromotionBranchesCommand>
{
    public ChangePromotionBranchesValidator()
    {
        RuleFor(command => command.PromotionId).NotEmpty();

        // "All branches" and a list of branches contradict each other: a malformed request, not a business rule.
        RuleFor(command => command.BranchIds)
            .Must(ids => ids is null or { Count: 0 })
            .When(command => command.AppliesToAllBranches);
        RuleFor(command => command.BranchIds).NotEmpty().When(command => !command.AppliesToAllBranches);
        RuleForEach(command => command.BranchIds!).NotEqual(Guid.Empty); // a null list has nothing to iterate
    }
}