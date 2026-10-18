using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Customers;

public class CustomerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private static Email Mail(string value = "ana@exemplo.com") => Email.Create(value);

    private static Phone Tel(string value = "+5511987654321") => Phone.Create(value);

    private static Customer CreateCustomer()
    {
        var customer = Customer.Register(CompanyId, Mail(), Tel(), "Ana Souza");
        customer.ClearDomainEvents();
        return customer;
    }

    private static int UpdatedEvents(Customer customer) => customer.DomainEvents.OfType<CustomerUpdated>().Count();

    [Fact]
    public void Register_should_create_the_customer_with_defaults_and_raise_the_registered_event()
    {
        var customer = Customer.Register(CompanyId, Mail(), Tel(), "  Ana Souza ");

        customer.Id.Version.ShouldBe(7);
        customer.CompanyId.ShouldBe(CompanyId);
        customer.Name.ShouldBe("Ana Souza");
        customer.Email!.Value.ShouldBe("ana@exemplo.com");
        customer.Phone!.Value.ShouldBe("+5511987654321");
        customer.Cpf.ShouldBeNull();
        customer.BirthDate.ShouldBeNull();
        customer.LastAccessAt.ShouldBeNull();
        customer.AcceptsMarketing.ShouldBeFalse();
        customer.PreferredLanguage.ShouldBeNull();

        var registered = customer.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CustomerRegistered>();
        registered.CustomerId.ShouldBe(customer.Id);
        registered.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Customer_should_be_a_tenant_entity()
    {
        ITenantEntity tenantEntity = CreateCustomer();

        tenantEntity.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Register_with_only_the_phone_should_be_accepted_without_a_name()
    {
        var customer = Customer.Register(CompanyId, null, Tel());

        customer.Email.ShouldBeNull();
        customer.Name.ShouldBeNull();
    }

    [Fact]
    public void Register_with_only_the_email_should_be_accepted()
    {
        var customer = Customer.Register(CompanyId, Mail(), null);

        customer.Phone.ShouldBeNull();
    }

    [Fact]
    public void Register_without_any_contact_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Customer.Register(CompanyId, null, null))
            .Code.ShouldBe("Customer.ContactRequired");
    }

    [Fact]
    public void Register_with_empty_company_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Customer.Register(Guid.Empty, Mail(), Tel()))
            .Code.ShouldBe("Customer.CompanyRequired");
    }

    [Fact]
    public void Register_should_keep_the_consent_and_language_given()
    {
        var customer = Customer.Register(CompanyId, Mail(), null, acceptsMarketing: true, preferredLanguage: Language.English);

        customer.AcceptsMarketing.ShouldBeTrue();
        customer.PreferredLanguage.ShouldBe(Language.English);
    }

    [Fact]
    public void Name_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Customer.Register(CompanyId, Mail(), null, new string('a', 151)))
            .Code.ShouldBe("Customer.NameInvalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_become_null(string name)
    {
        Customer.Register(CompanyId, Mail(), null, name).Name.ShouldBeNull();
    }

    [Fact]
    public void UpdateContact_should_change_the_contacts_and_raise_the_updated_event()
    {
        var customer = CreateCustomer();

        customer.UpdateContact(Mail("novo@exemplo.com"), Tel("+5511911112222"));

        customer.Email!.Value.ShouldBe("novo@exemplo.com");
        customer.Phone!.Value.ShouldBe("+5511911112222");
        UpdatedEvents(customer).ShouldBe(1);
    }

    [Fact]
    public void UpdateContact_with_the_same_values_should_raise_no_event()
    {
        var customer = CreateCustomer();

        customer.UpdateContact(Mail(), Tel());

        UpdatedEvents(customer).ShouldBe(0);
    }

    [Fact]
    public void UpdateContact_may_remove_one_contact_but_not_both()
    {
        var customer = CreateCustomer();

        customer.UpdateContact(Mail(), null);
        customer.Phone.ShouldBeNull();

        Should.Throw<DomainException>(() => customer.UpdateContact(null, null))
            .Code.ShouldBe("Customer.ContactRequired");
        customer.Email.ShouldNotBeNull();
    }

    [Fact]
    public void UpdateProfile_should_change_the_profile_and_raise_the_updated_event()
    {
        var customer = CreateCustomer();
        var cpf = Cpf.Create("529.982.247-25");

        customer.UpdateProfile("Ana Maria Souza", cpf, new DateOnly(1989, 5, 12), Language.Spanish);

        customer.Name.ShouldBe("Ana Maria Souza");
        customer.Cpf.ShouldBe(cpf);
        customer.BirthDate.ShouldBe(new DateOnly(1989, 5, 12));
        customer.PreferredLanguage.ShouldBe(Language.Spanish);
        UpdatedEvents(customer).ShouldBe(1);
    }

    [Fact]
    public void UpdateProfile_with_the_same_values_should_raise_no_new_event()
    {
        var customer = CreateCustomer();
        customer.UpdateProfile("Ana Souza", null, new DateOnly(1989, 5, 12), null);
        customer.ClearDomainEvents();

        customer.UpdateProfile("Ana Souza", null, new DateOnly(1989, 5, 12), null);

        UpdatedEvents(customer).ShouldBe(0);
    }

    [Fact]
    public void Invalid_birth_date_should_be_rejected_and_change_nothing()
    {
        var customer = CreateCustomer();

        Should.Throw<DomainException>(() =>
                customer.UpdateProfile("Outro Nome", null, new DateOnly(1899, 12, 31), null))
            .Code.ShouldBe("Customer.BirthDateInvalid");

        customer.Name.ShouldBe("Ana Souza");
        UpdatedEvents(customer).ShouldBe(0);
    }

    [Fact]
    public void Invalid_name_in_UpdateProfile_should_be_rejected_and_change_nothing()
    {
        var customer = CreateCustomer();

        Should.Throw<DomainException>(() =>
                customer.UpdateProfile(new string('a', 151), null, new DateOnly(1989, 5, 12), null))
            .Code.ShouldBe("Customer.NameInvalid");

        customer.BirthDate.ShouldBeNull();
    }

    [Fact]
    public void ChangeMarketingConsent_should_raise_the_event_only_when_the_value_changes()
    {
        var customer = CreateCustomer();

        customer.ChangeMarketingConsent(false);
        UpdatedEvents(customer).ShouldBe(0);

        customer.ChangeMarketingConsent(true);
        customer.AcceptsMarketing.ShouldBeTrue();
        UpdatedEvents(customer).ShouldBe(1);
    }

    [Fact]
    public void RegisterAccess_should_store_the_utc_time_without_raising_events()
    {
        var customer = CreateCustomer();
        var accessedAt = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);

        customer.RegisterAccess(accessedAt);

        customer.LastAccessAt.ShouldBe(accessedAt);
        customer.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RegisterAccess_with_a_non_utc_time_should_be_rejected()
    {
        var customer = CreateCustomer();

        Should.Throw<DomainException>(() =>
                customer.RegisterAccess(new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Local)))
            .Code.ShouldBe("Customer.AccessTimeInvalid");

        customer.LastAccessAt.ShouldBeNull();
    }
}