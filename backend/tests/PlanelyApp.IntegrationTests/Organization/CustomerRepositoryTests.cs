using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class CustomerRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_customer_should_come_back_with_the_profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var accessedAt = new DateTime(2027, 5, 20, 14, 30, 0, DateTimeKind.Utc);
        var cpf = StaffData.NewCpf();

        var customer = StaffData.NewCustomer(company.Id);
        customer.UpdateProfile("João da Silva", cpf, new DateOnly(1990, 5, 17), Language.English);
        customer.RegisterAccess(accessedAt);
        customer.ChangeMarketingConsent(false);
        await SaveAsync(company.Id, customer, cancellationToken);

        var loaded = await LoadAsync(company.Id, customer.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Name.ShouldBe("João da Silva");
        loaded.Email.ShouldBe(customer.Email);
        loaded.Phone.ShouldBe(customer.Phone);
        loaded.Cpf.ShouldBe(cpf);
        loaded.BirthDate.ShouldBe(new DateOnly(1990, 5, 17));
        loaded.PreferredLanguage.ShouldBe(Language.English);
        loaded.AcceptsMarketing.ShouldBeFalse();
        loaded.LastAccessAt.ShouldBe(accessedAt);
        loaded.LastAccessAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task A_customer_should_be_found_by_contact_inside_its_own_company_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var cpf = StaffData.NewCpf();

        var customer = StaffData.NewCustomer(company.Id);
        customer.UpdateProfile("Karen Faria", cpf, null, null);
        await SaveAsync(company.Id, customer, cancellationToken);

        await using var scope = mysql.CreateScope(company.Id);
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        (await repository.FindByEmailAsync(company.Id, customer.Email!, cancellationToken))!.Id.ShouldBe(customer.Id);
        (await repository.FindByPhoneAsync(company.Id, customer.Phone!, cancellationToken))!.Id.ShouldBe(customer.Id);
        (await repository.ExistsByCpfAsync(company.Id, cpf, cancellationToken)).ShouldBeTrue();

        (await repository.FindByEmailAsync(company.Id, Email.Create(StaffData.NewEmail()), cancellationToken)).ShouldBeNull();
        (await repository.ExistsByCpfAsync(company.Id, StaffData.NewCpf(), cancellationToken)).ShouldBeFalse();

        // Another company asking for the same contact does not see this customer.
        (await repository.FindByEmailAsync(other.Id, customer.Email!, cancellationToken)).ShouldBeNull();
        (await repository.FindByPhoneAsync(other.Id, customer.Phone!, cancellationToken)).ShouldBeNull();
        (await repository.ExistsByCpfAsync(other.Id, cpf, cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_phone_should_be_used_once_per_company_but_may_repeat_across_companies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var phone = StaffData.NewPhone();

        await SaveAsync(company.Id, StaffData.NewCustomer(company.Id, phone: phone), cancellationToken);
        await SaveAsync(other.Id, StaffData.NewCustomer(other.Id, phone: phone), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(company.Id, StaffData.NewCustomer(company.Id, phone: phone), cancellationToken));
    }

    [Fact]
    public async Task Customers_without_an_email_should_not_conflict_with_each_other()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await SaveAsync(company.Id, Customer.Register(company.Id, null, StaffData.NewPhone()), cancellationToken);
        await SaveAsync(company.Id, Customer.Register(company.Id, null, StaffData.NewPhone()), cancellationToken);
    }

    [Fact]
    public async Task The_longest_name_the_domain_allows_should_fit_in_the_column()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var customer = Customer.Register(company.Id, null, StaffData.NewPhone(), new string('N', 150));
        await SaveAsync(company.Id, customer, cancellationToken);

        var loaded = await LoadAsync(company.Id, customer.Id, cancellationToken);

        loaded!.Name!.Length.ShouldBe(150);
    }

    private Task SaveAsync(Guid companyId, Customer customer, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(customer));

    private async Task<Customer?> LoadAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<ICustomerRepository>().GetByIdAsync(id, cancellationToken);
    }
}