using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Organization.Customers;
using PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;
using PlanelyApp.Application.Organization.Customers.Get;
using PlanelyApp.Application.Organization.Customers.List;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Application.Organization.Customers.UpdateContact;
using PlanelyApp.Application.Organization.Customers.UpdateProfile;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class CustomerUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_registered_customer_should_be_saved_normalized_and_read_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var registered = await mysql.SendAsync(
            company.Id,
            new RegisterCustomerCommand("Ana Souza", " Ana@Example.com ", "+55 (11) 91111-0001", true, "en"),
            cancellationToken);

        registered.IsSuccess.ShouldBeTrue();
        var dto = (await mysql.QueryAsync(company.Id, new GetCustomerQuery(registered.Value), cancellationToken)).Value;
        dto.Id.ShouldBe(registered.Value);
        dto.Name.ShouldBe("Ana Souza");
        dto.Email.ShouldBe("ana@example.com");
        dto.Phone.ShouldBe("+5511911110001");
        dto.AcceptsMarketing.ShouldBeTrue();
        dto.PreferredLanguage.ShouldBe("en");
        dto.Cpf.ShouldBeNull();
        dto.BirthDate.ShouldBeNull();
        dto.LastAccessAt.ShouldBeNull();
    }

    [Fact]
    public async Task E_mail_and_phone_should_be_unique_inside_the_company_but_free_in_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", "+5511911110001", cancellationToken);

        var sameEmail = await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Other", "ANA@example.com", "+5511911110002"), cancellationToken);
        var samePhone = await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Other", "other@example.com", "+5511911110001"), cancellationToken);
        var inAnotherCompany = await mysql.SendAsync(
            other.Id, new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511911110001"), cancellationToken);

        sameEmail.Error.Code.ShouldBe("Customer.EmailAlreadyRegistered");
        samePhone.Error.Code.ShouldBe("Customer.PhoneAlreadyRegistered");
        inAnotherCompany.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_contact_should_clash_with_another_customer_but_never_with_the_own_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", "+5511911110001", cancellationToken);
        var bruno = await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", "+5511911110002", cancellationToken);

        var emailTaken = await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(bruno, "ana@example.com", "+5511911110002"), cancellationToken);
        var phoneTaken = await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(bruno, "bruno@example.com", "+5511911110001"), cancellationToken);
        var unchanged = await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(bruno, "bruno@example.com", "+5511911110002"), cancellationToken);
        var changed = await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(bruno, "bruno.novo@example.com", null), cancellationToken);

        emailTaken.Error.Code.ShouldBe("Customer.EmailAlreadyRegistered");
        phoneTaken.Error.Code.ShouldBe("Customer.PhoneAlreadyRegistered");
        unchanged.IsSuccess.ShouldBeTrue();
        changed.IsSuccess.ShouldBeTrue();
        var dto = (await mysql.QueryAsync(company.Id, new GetCustomerQuery(bruno), cancellationToken)).Value;
        dto.Email.ShouldBe("bruno.novo@example.com");
        dto.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task A_cpf_should_be_unique_inside_the_company_and_come_back_formatted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var ana = await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", "+5511911110001", cancellationToken);
        var bruno = await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", "+5511911110002", cancellationToken);
        var carla = await RegisterAsync(other.Id, "Carla Dias", "carla@example.com", "+5511911110003", cancellationToken);

        var first = await mysql.SendAsync(
            company.Id,
            new UpdateCustomerProfileCommand(ana, "Ana Souza", "529.982.247-25", new DateOnly(1990, 5, 17), "pt-BR"),
            cancellationToken);
        var taken = await mysql.SendAsync(
            company.Id, new UpdateCustomerProfileCommand(bruno, "Bruno Alves", "52998224725", null, null), cancellationToken);
        var again = await mysql.SendAsync(
            company.Id,
            new UpdateCustomerProfileCommand(ana, "Ana Maria", "529.982.247-25", new DateOnly(1990, 5, 17), "pt-BR"),
            cancellationToken);
        var inAnotherCompany = await mysql.SendAsync(
            other.Id, new UpdateCustomerProfileCommand(carla, "Carla Dias", "529.982.247-25", null, null), cancellationToken);

        first.IsSuccess.ShouldBeTrue();
        taken.Error.Code.ShouldBe("Customer.CpfAlreadyRegistered");
        again.IsSuccess.ShouldBeTrue();
        inAnotherCompany.IsSuccess.ShouldBeTrue();
        var dto = (await mysql.QueryAsync(company.Id, new GetCustomerQuery(ana), cancellationToken)).Value;
        dto.Name.ShouldBe("Ana Maria");
        dto.Cpf.ShouldBe("529.982.247-25");
        dto.BirthDate.ShouldBe(new DateOnly(1990, 5, 17));
        dto.PreferredLanguage.ShouldBe("pt-BR");
    }

    [Fact]
    public async Task The_marketing_consent_should_be_saved_both_ways()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", null, cancellationToken);

        (await mysql.SendAsync(company.Id, new ChangeCustomerMarketingConsentCommand(id, true), cancellationToken))
            .IsSuccess.ShouldBeTrue();
        var given = (await mysql.QueryAsync(company.Id, new GetCustomerQuery(id), cancellationToken)).Value;

        (await mysql.SendAsync(company.Id, new ChangeCustomerMarketingConsentCommand(id, false), cancellationToken))
            .IsSuccess.ShouldBeTrue();
        var withdrawn = (await mysql.QueryAsync(company.Id, new GetCustomerQuery(id), cancellationToken)).Value;

        given.AcceptsMarketing.ShouldBeTrue();
        withdrawn.AcceptsMarketing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_customer_of_another_company_should_not_be_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", null, cancellationToken);

        var get = await mysql.QueryAsync(other.Id, new GetCustomerQuery(id), cancellationToken);
        var contact = await mysql.SendAsync(
            other.Id, new UpdateCustomerContactCommand(id, "ana@example.com", null), cancellationToken);
        var profile = await mysql.SendAsync(
            other.Id, new UpdateCustomerProfileCommand(id, "Ana", null, null, null), cancellationToken);
        var consent = await mysql.SendAsync(
            other.Id, new ChangeCustomerMarketingConsentCommand(id, true), cancellationToken);

        get.Error.Code.ShouldBe("Customer.NotFound");
        contact.Error.Code.ShouldBe("Customer.NotFound");
        profile.Error.Code.ShouldBe("Customer.NotFound");
        consent.Error.Code.ShouldBe("Customer.NotFound");
    }

    [Fact]
    public async Task The_list_should_be_paged_by_name_with_the_unnamed_last()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Carla Dias", "carla@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, null, null, "+5511911110009", cancellationToken);
        await RegisterAsync(company.Id, "Daniel Reis", "daniel@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", null, cancellationToken);

        var first = (await mysql.QueryAsync(company.Id, new ListCustomersQuery(Page: 1, PageSize: 2), cancellationToken)).Value;
        var second = (await mysql.QueryAsync(company.Id, new ListCustomersQuery(Page: 2, PageSize: 2), cancellationToken)).Value;
        var third = (await mysql.QueryAsync(company.Id, new ListCustomersQuery(Page: 3, PageSize: 2), cancellationToken)).Value;

        first.Items.Select(customer => customer.Name).ShouldBe(["Ana Souza", "Bruno Alves"], ignoreOrder: false);
        second.Items.Select(customer => customer.Name).ShouldBe(["Carla Dias", "Daniel Reis"], ignoreOrder: false);
        var unnamed = third.Items.ShouldHaveSingleItem();
        unnamed.Name.ShouldBeNull();
        unnamed.Phone.ShouldBe("+5511911110009");
        unnamed.Email.ShouldBeNull();
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(2);
        first.TotalCount.ShouldBe(5);
        first.TotalPages.ShouldBe(3);
        first.Items[0].Email.ShouldBe("ana@example.com");
        first.Items[0].AcceptsMarketing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_page_after_the_last_should_be_empty_but_keep_the_total()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, "Carla Dias", "carla@example.com", null, cancellationToken);

        var page = (await mysql.QueryAsync(company.Id, new ListCustomersQuery(Page: 5, PageSize: 2), cancellationToken)).Value;

        page.Items.ShouldBeEmpty();
        page.Page.ShouldBe(5);
        page.TotalCount.ShouldBe(3);
        page.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task The_search_should_find_a_piece_of_the_name_and_the_whole_email_or_phone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", "+5511911110001", cancellationToken);
        await RegisterAsync(company.Id, "Mariana Lima", "mariana@example.com", "+5511911110002", cancellationToken);
        await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", "+5511911110003", cancellationToken);

        var byName = await SearchAsync(company.Id, "ana", cancellationToken);
        var byNameUpper = await SearchAsync(company.Id, "ANA", cancellationToken);
        var byEmail = await SearchAsync(company.Id, "BRUNO@example.com", cancellationToken);
        var byPhone = await SearchAsync(company.Id, "+55 11 91111-0003", cancellationToken);
        var byNothing = await SearchAsync(company.Id, "nobody", cancellationToken);

        byName.Items.Select(customer => customer.Name).ShouldBe(["Ana Souza", "Mariana Lima"], ignoreOrder: false);
        byName.TotalCount.ShouldBe(2);
        byNameUpper.Items.Select(customer => customer.Name).ShouldBe(["Ana Souza", "Mariana Lima"], ignoreOrder: false);
        byEmail.Items.ShouldHaveSingleItem().Name.ShouldBe("Bruno Alves");
        byPhone.Items.ShouldHaveSingleItem().Name.ShouldBe("Bruno Alves");
        byNothing.Items.ShouldBeEmpty();
        byNothing.TotalCount.ShouldBe(0);
        byNothing.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task The_list_should_show_only_the_customers_of_the_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", "ana@example.com", null, cancellationToken);
        await RegisterAsync(company.Id, "Bruno Alves", "bruno@example.com", null, cancellationToken);
        await RegisterAsync(other.Id, "Carla Dias", "carla@example.com", null, cancellationToken);

        var ofCompany = (await mysql.QueryAsync(company.Id, new ListCustomersQuery(), cancellationToken)).Value;
        var ofOther = (await mysql.QueryAsync(other.Id, new ListCustomersQuery(), cancellationToken)).Value;

        ofCompany.TotalCount.ShouldBe(2);
        ofCompany.Items.Select(customer => customer.Name).ShouldBe(["Ana Souza", "Bruno Alves"], ignoreOrder: false);
        ofOther.Items.ShouldHaveSingleItem().Name.ShouldBe("Carla Dias");
    }

    private async Task<Guid> RegisterAsync(
        Guid companyId,
        string? name,
        string? email,
        string? phone,
        CancellationToken cancellationToken)
    {
        var result = await mysql.SendAsync(companyId, new RegisterCustomerCommand(name, email, phone), cancellationToken);

        return result.Value;
    }

    private async Task<PagedResult<CustomerSummaryDto>> SearchAsync(
        Guid companyId,
        string search,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new ListCustomersQuery(search), cancellationToken)).Value;
}