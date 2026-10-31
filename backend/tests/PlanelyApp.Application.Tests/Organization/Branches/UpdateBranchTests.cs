using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class UpdateBranchTests
{
    private static UpdateBranchDetailsCommand NewDetails(Guid branchId) => new(
        branchId,
        "Centro Novo",
        new AddressData("Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", "01310-100"),
        "+551144445555",
        "America/Manaus");

    [Fact]
    public async Task The_details_should_be_updated()
    {
        var branch = BranchData.NewBranch();
        var branches = BranchData.RepositoryWith(branch);

        var result = await new UpdateBranchDetailsHandler(branches)
            .HandleAsync(NewDetails(branch.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.Name.ShouldBe("Centro Novo");
        branch.Address.Street.ShouldBe("Avenida Paulista");
        branch.Address.Complement.ShouldBeNull();
        branch.Address.PostalCode.ShouldBe("01310100");
        branch.Phone.Value.ShouldBe("+551144445555");
        branch.TimeZoneId.ShouldBe("America/Manaus");
        await branches.Received().ExistsByNameAsync("Centro Novo", branch.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found()
    {
        var result = await new UpdateBranchDetailsHandler(BranchData.RepositoryWith(null))
            .HandleAsync(NewDetails(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_name_used_by_another_branch_should_be_a_conflict_and_change_nothing()
    {
        var branch = BranchData.NewBranch();
        var branches = BranchData.RepositoryWith(branch);
        branches.ExistsByNameAsync("Centro Novo", branch.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new UpdateBranchDetailsHandler(branches)
            .HandleAsync(NewDetails(branch.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Branch.NameAlreadyExists");
        branch.Name.ShouldBe("Centro");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_and_leave_the_branch_untouched()
    {
        var branch = BranchData.NewBranch();
        var handler = new UpdateBranchDetailsHandler(BranchData.RepositoryWith(branch));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            NewDetails(branch.Id) with { Name = new string('x', 101) }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.NameInvalid");
        branch.Name.ShouldBe("Centro");
        branch.TimeZoneId.ShouldBe(BranchData.ValidTimeZone);
    }

    [Fact]
    public async Task The_validator_should_require_every_part()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateBranchDetailsValidator(), new UpdateBranchDetailsCommand(Guid.NewGuid(), "", null!, "", ""));

        problems.ShouldBe(
            [
                "Name:Validation.Required",
                "Address:Validation.Required",
                "Phone:Validation.Required",
                "TimeZoneId:Validation.Required",
            ],
            ignoreOrder: true);
    }
}