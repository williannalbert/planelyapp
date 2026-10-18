using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Companies;

public class CompanyTests
{
    private static Company CreateCompany(
        string? tradeName = "Studio Aurora",
        string? legalName = "Aurora Serviços Ltda",
        BookingPolicy? policy = null) =>
        Company.Create(
            tradeName,
            legalName,
            Cnpj.Create("11.222.333/0001-81"),
            Email.Create("contato@aurora.com"),
            Phone.Create("+5511987654321"),
            Language.Default,
            policy);

    [Fact]
    public void Create_should_register_an_active_company_with_default_policy()
    {
        var company = CreateCompany(tradeName: "  Studio Aurora ");

        company.Id.Version.ShouldBe(7);
        company.TradeName.ShouldBe("Studio Aurora");
        company.LegalName.ShouldBe("Aurora Serviços Ltda");
        company.Cnpj.Value.ShouldBe("11222333000181");
        company.Email.Value.ShouldBe("contato@aurora.com");
        company.Phone.Value.ShouldBe("+5511987654321");
        company.DefaultLanguage.ShouldBe(Language.PortugueseBrazil);
        company.BookingPolicy.ShouldBe(BookingPolicy.Default);
        company.IsActive.ShouldBeTrue();
        company.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_should_use_the_given_policy()
    {
        var policy = BookingPolicy.Create(4, 30, 12, 15, 45, true);

        CreateCompany(policy: policy).BookingPolicy.ShouldBe(policy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_trade_name_should_be_rejected(string? tradeName)
    {
        Should.Throw<DomainException>(() => CreateCompany(tradeName: tradeName))
            .Code.ShouldBe("Company.TradeNameInvalid");
    }

    [Fact]
    public void Blank_legal_name_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateCompany(legalName: " "))
            .Code.ShouldBe("Company.LegalNameInvalid");
    }

    [Fact]
    public void Names_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateCompany(tradeName: new string('a', 151)))
            .Code.ShouldBe("Company.TradeNameInvalid");
        Should.Throw<DomainException>(() => CreateCompany(legalName: new string('a', 201)))
            .Code.ShouldBe("Company.LegalNameInvalid");
    }

    [Fact]
    public void UpdateDetails_should_change_the_company_data_but_not_the_cnpj()
    {
        var company = CreateCompany();

        company.UpdateDetails(
            "Aurora Beauty",
            "Aurora Beauty Ltda",
            Email.Create("novo@aurora.com"),
            Phone.Create("+5511912345678"),
            Language.English);

        company.TradeName.ShouldBe("Aurora Beauty");
        company.LegalName.ShouldBe("Aurora Beauty Ltda");
        company.Email.Value.ShouldBe("novo@aurora.com");
        company.Phone.Value.ShouldBe("+5511912345678");
        company.DefaultLanguage.ShouldBe(Language.English);
        company.Cnpj.Value.ShouldBe("11222333000181");
    }

    [Fact]
    public void Invalid_update_should_leave_the_company_unchanged()
    {
        var company = CreateCompany();

        Should.Throw<DomainException>(() => company.UpdateDetails(
            "Aurora Beauty",
            " ",
            Email.Create("novo@aurora.com"),
            Phone.Create("+5511912345678"),
            Language.English));

        company.TradeName.ShouldBe("Studio Aurora");
        company.Email.Value.ShouldBe("contato@aurora.com");
        company.DefaultLanguage.ShouldBe(Language.PortugueseBrazil);
    }

    [Fact]
    public void UpdatePolicy_should_replace_the_policy()
    {
        var company = CreateCompany();
        var policy = BookingPolicy.Create(1, 90, 48, 10, 60, true);

        company.UpdatePolicy(policy);

        company.BookingPolicy.ShouldBe(policy);
    }

    [Fact]
    public void Deactivate_and_activate_should_toggle_the_flag()
    {
        var company = CreateCompany();

        company.Deactivate();
        company.IsActive.ShouldBeFalse();

        company.Activate();
        company.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Deactivating_twice_should_keep_the_company_inactive()
    {
        var company = CreateCompany();

        company.Deactivate();
        company.Deactivate();

        company.IsActive.ShouldBeFalse();
    }
}