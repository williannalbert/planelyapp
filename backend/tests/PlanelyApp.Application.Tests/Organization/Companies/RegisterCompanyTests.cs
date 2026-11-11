using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.Register;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Companies;

public class RegisterCompanyTests
{
    private readonly ICompanyRepository _companies = Substitute.For<ICompanyRepository>();

    [Fact]
    public async Task A_new_company_should_be_added_with_the_default_policy()
    {
        var added = CaptureAdded();

        var result = await new RegisterCompanyHandler(_companies)
            .HandleAsync(CompanyData.RegisterCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added().ShouldNotBeNull();
        result.Value.ShouldBe(added()!.Id);
        added()!.TradeName.ShouldBe("Acme");
        added()!.Cnpj.Value.ShouldBe(CompanyData.ValidCnpj);
        added()!.DefaultLanguage.ShouldBe(Language.English);
        added()!.BookingPolicy.ShouldBe(BookingPolicy.Default);
    }

    [Fact]
    public async Task A_policy_in_the_command_should_replace_the_default()
    {
        var added = CaptureAdded();
        var policy = new BookingPolicyData(4, 90, 12, 10, 45, true);

        await new RegisterCompanyHandler(_companies)
            .HandleAsync(CompanyData.RegisterCommand(policy), TestContext.Current.CancellationToken);

        added()!.BookingPolicy.ShouldBe(BookingPolicy.Create(4, 90, 12, 10, 45, true));
    }

    [Fact]
    public async Task A_cnpj_already_registered_should_be_a_conflict_and_add_nothing()
    {
        _companies.ExistsByCnpjAsync(Arg.Any<Cnpj>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await new RegisterCompanyHandler(_companies)
            .HandleAsync(CompanyData.RegisterCommand(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Company.CnpjAlreadyRegistered");
        _companies.DidNotReceive().Add(Arg.Any<Company>());
    }

    [Fact]
    public async Task A_limit_of_the_domain_should_be_refused_with_the_code_of_the_field()
    {
        var command = CompanyData.RegisterCommand() with { TradeName = new string('x', 151) };

        var exception = await Should.ThrowAsync<DomainException>(() => new RegisterCompanyHandler(_companies)
            .HandleAsync(command, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Company.TradeNameInvalid");
        _companies.DidNotReceive().Add(Arg.Any<Company>());
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new RegisterCompanyValidator(), CompanyData.RegisterCommand());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Empty_fields_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterCompanyValidator(), new RegisterCompanyCommand("", "", "", "", "", ""));

        problems.ShouldBe(
            [
                "TradeName:Validation.Required",
                "LegalName:Validation.Required",
                "Cnpj:Validation.Required",
                "Email:Validation.Required",
                "Phone:Validation.Required",
                "DefaultLanguage:Validation.Required",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Values_a_value_object_refuses_should_be_reported_as_invalid()
    {
        var command = new RegisterCompanyCommand("Acme", "Acme LTDA", "123", "not-an-email", "abc", "xx");

        var problems = await TestValidation.RunAsync(new RegisterCompanyValidator(), command);

        problems.ShouldBe(
            [
                "Cnpj:Validation.Invalid",
                "Email:Validation.Invalid",
                "Phone:Validation.Invalid",
                "DefaultLanguage:Validation.Invalid",
            ],
            ignoreOrder: true);
    }

    private Func<Company?> CaptureAdded()
    {
        Company? added = null;
        _companies.When(repository => repository.Add(Arg.Any<Company>())).Do(call => added = call.Arg<Company>());

        return () => added;
    }
}