using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions.ChangeBranches;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Organization.Branches;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class ChangePromotionBranchesTests
{
    private readonly IBranchRepository _branches = Substitute.For<IBranchRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task Moving_to_some_branches_should_replace_where_the_promotion_applies()
    {
        var promotion = PromotionData.NewPromotion(_companyId);
        var (branchA, branchB) = (Guid.NewGuid(), Guid.NewGuid());
        BranchesFound(2);

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionBranchesCommand(promotion.Id, false, [branchA, branchB]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.AppliesToAllBranches.ShouldBeFalse();
        promotion.BranchIds.ShouldBe([branchA, branchB], ignoreOrder: false);
    }

    [Fact]
    public async Task Repeated_branch_ids_should_count_once()
    {
        var promotion = PromotionData.NewPromotion(_companyId);
        var (branchA, branchB) = (Guid.NewGuid(), Guid.NewGuid());
        BranchesFound(2);

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionBranchesCommand(promotion.Id, false, [branchA, branchA, branchB]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.BranchIds.ShouldBe([branchA, branchB], ignoreOrder: false);
    }

    [Fact]
    public async Task Moving_to_all_branches_should_forget_the_list_and_not_look_for_branches()
    {
        var promotion = PromotionData.NewPromotionForBranches(_companyId, Guid.NewGuid());

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionBranchesCommand(promotion.Id, true), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        promotion.AppliesToAllBranches.ShouldBeTrue();
        promotion.BranchIds.ShouldBeEmpty();
        await _branches.DidNotReceive().CountByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_promotion_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new ChangePromotionBranchesCommand(Guid.NewGuid(), true), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Promotion.NotFound");
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found_and_leave_the_promotion_alone()
    {
        var promotion = PromotionData.NewPromotion(_companyId);
        BranchesFound(1);

        var result = await Handler(promotion).HandleAsync(
            new ChangePromotionBranchesCommand(promotion.Id, false, [Guid.NewGuid(), Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        promotion.AppliesToAllBranches.ShouldBeTrue();
        promotion.BranchIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionBranchesValidator(),
            new ChangePromotionBranchesCommand(Guid.NewGuid(), false, [Guid.NewGuid()]));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task All_branches_together_with_a_list_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionBranchesValidator(),
            new ChangePromotionBranchesCommand(Guid.NewGuid(), true, [Guid.NewGuid()]));

        problems.ShouldBe(["BranchIds:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task Some_branches_without_a_list_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionBranchesValidator(), new ChangePromotionBranchesCommand(Guid.NewGuid(), false));

        problems.ShouldBe(["BranchIds:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_branch_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionBranchesValidator(),
            new ChangePromotionBranchesCommand(Guid.NewGuid(), false, [Guid.Empty]));

        problems.ShouldBe(["BranchIds[0]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ChangePromotionBranchesValidator(), new ChangePromotionBranchesCommand(Guid.Empty, true));

        problems.ShouldBe(["PromotionId:Validation.Required"], ignoreOrder: true);
    }

    private void BranchesFound(int count) =>
        _branches.CountByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(count);

    private ChangePromotionBranchesHandler Handler(Promotion? promotion) =>
        new(PromotionData.PromotionsWith(promotion), _branches);
}