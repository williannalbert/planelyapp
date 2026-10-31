using NSubstitute;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class RegisterProfessionalTests
{
    private readonly IProfessionalRepository _professionals = Substitute.For<IProfessionalRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_new_professional_should_be_added_to_the_company_of_the_request()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new RegisterProfessionalCommand("  Ana Souza  ", "Cabeleireira"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added().ShouldNotBeNull();
        result.Value.ShouldBe(added()!.Id);
        added()!.CompanyId.ShouldBe(_companyId);
        added()!.Name.ShouldBe("Ana Souza");
        added()!.Specialty.ShouldBe("Cabeleireira");
        added()!.IsActive.ShouldBeTrue();
        added()!.UserId.ShouldBeNull();
        added()!.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task A_blank_specialty_should_be_stored_as_none()
    {
        var added = CaptureAdded();

        await Handler().HandleAsync(
            new RegisterProfessionalCommand("Ana Souza", "   "), TestContext.Current.CancellationToken);

        added()!.Specialty.ShouldBeNull();
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RegisterProfessionalCommand(new string('x', 101)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Professional.NameInvalid");
        _professionals.DidNotReceive().Add(Arg.Any<Professional>());
    }

    [Fact]
    public async Task A_specialty_above_the_limit_should_be_refused_by_the_domain()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RegisterProfessionalCommand("Ana Souza", new string('x', 101)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Professional.SpecialtyInvalid");
        _professionals.DidNotReceive().Add(Arg.Any<Professional>());
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var handler = new RegisterProfessionalHandler(_professionals, Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RegisterProfessionalCommand("Ana Souza"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterProfessionalValidator(), new RegisterProfessionalCommand("Ana Souza", "Cabeleireira"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_name_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterProfessionalValidator(), new RegisterProfessionalCommand(""));

        problems.ShouldBe(["Name:Validation.Required"], ignoreOrder: true);
    }

    private RegisterProfessionalHandler Handler() => new(_professionals, BranchData.TenantOf(_companyId));

    private Func<Professional?> CaptureAdded()
    {
        Professional? added = null;
        _professionals.When(repository => repository.Add(Arg.Any<Professional>()))
            .Do(call => added = call.Arg<Professional>());

        return () => added;
    }
}