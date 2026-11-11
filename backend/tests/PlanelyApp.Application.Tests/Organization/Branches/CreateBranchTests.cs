using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class CreateBranchTests
{
    private readonly IBranchRepository _branches = Substitute.For<IBranchRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_new_branch_should_be_added_to_the_company_of_the_request()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            BranchData.CreateCommand("  Centro  "), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added().ShouldNotBeNull();
        result.Value.ShouldBe(added()!.Id);
        added()!.CompanyId.ShouldBe(_companyId);
        added()!.Name.ShouldBe("Centro");
        added()!.Address.PostalCode.ShouldBe("01310100");
        added()!.Phone.Value.ShouldBe(BranchData.ValidPhone);
        added()!.TimeZoneId.ShouldBe(BranchData.ValidTimeZone);
        added()!.IsActive.ShouldBeTrue();
        await _branches.Received().ExistsByNameAsync("Centro", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_name_already_used_in_the_company_should_be_a_conflict_and_add_nothing()
    {
        _branches.ExistsByNameAsync("Centro", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(BranchData.CreateCommand(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Branch.NameAlreadyExists");
        _branches.DidNotReceive().Add(Arg.Any<Branch>());
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly_before_touching_the_repository()
    {
        var handler = new CreateBranchHandler(_branches, Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.HandleAsync(BranchData.CreateCommand(), TestContext.Current.CancellationToken));

        await _branches.DidNotReceive().ExistsByNameAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_time_zone_should_be_refused_by_the_domain()
    {
        var command = BranchData.CreateCommand() with { TimeZoneId = "Mars/Olympus" };

        var exception = await Should.ThrowAsync<DomainException>(
            () => Handler().HandleAsync(command, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.TimeZoneInvalid");
        _branches.DidNotReceive().Add(Arg.Any<Branch>());
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var command = BranchData.CreateCommand(new string('x', 101));

        var exception = await Should.ThrowAsync<DomainException>(
            () => Handler().HandleAsync(command, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.NameInvalid");
        _branches.DidNotReceive().Add(Arg.Any<Branch>());
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new CreateBranchValidator(), BranchData.CreateCommand());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_parts_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreateBranchValidator(), new CreateBranchCommand("", null!, "", ""));

        problems.ShouldBe(
            [
                "Name:Validation.Required",
                "Address:Validation.Required",
                "Phone:Validation.Required",
                "TimeZoneId:Validation.Required",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_address_and_a_bad_phone_should_say_which_field_is_wrong()
    {
        var command = new CreateBranchCommand(
            "Centro", new AddressData("", "", null, "", "", "", ""), "abc", BranchData.ValidTimeZone);

        var problems = await TestValidation.RunAsync(new CreateBranchValidator(), command);

        problems.ShouldBe(
            [
                "Address.Street:Validation.Required",
                "Address.Number:Validation.Required",
                "Address.District:Validation.Required",
                "Address.City:Validation.Required",
                "Address.State:Validation.Required",
                "Address.PostalCode:Validation.Required",
                "Phone:Validation.Invalid",
            ],
            ignoreOrder: true);
    }

    private CreateBranchHandler Handler() => new(_branches, BranchData.TenantOf(_companyId));

    private Func<Branch?> CaptureAdded()
    {
        Branch? added = null;
        _branches.When(repository => repository.Add(Arg.Any<Branch>())).Do(call => added = call.Arg<Branch>());

        return () => added;
    }
}