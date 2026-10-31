using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class RegisterCustomerTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_new_customer_should_be_added_to_the_company_of_the_request()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new RegisterCustomerCommand("  Ana Souza  ", " ANA@Example.com ", "+55 (11) 98888-7777", true, "EN"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added().ShouldNotBeNull();
        var customer = added()!;
        result.Value.ShouldBe(customer.Id);
        customer.CompanyId.ShouldBe(_companyId);
        customer.Name.ShouldBe("Ana Souza");
        customer.Email!.Value.ShouldBe("ana@example.com");
        customer.Phone!.Value.ShouldBe("+5511988887777");
        customer.AcceptsMarketing.ShouldBeTrue();
        customer.PreferredLanguage!.Code.ShouldBe("en");
        customer.Cpf.ShouldBeNull();
        customer.LastAccessAt.ShouldBeNull();
        customer.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CustomerRegistered>()
            .CustomerId.ShouldBe(customer.Id);
    }

    [Fact]
    public async Task A_phone_alone_should_be_enough_and_a_blank_email_should_mean_none()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new RegisterCustomerCommand(Email: "   ", Phone: CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.Email.ShouldBeNull();
        added()!.Phone!.Value.ShouldBe(CustomerData.ValidPhone);
        await _customers.DidNotReceive().FindByEmailAsync(
            Arg.Any<Guid>(), Arg.Any<Email>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_email_alone_should_be_enough_and_a_blank_phone_should_mean_none()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new RegisterCustomerCommand(Email: CustomerData.ValidEmail, Phone: ""),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.Phone.ShouldBeNull();
        await _customers.DidNotReceive().FindByPhoneAsync(
            Arg.Any<Guid>(), Arg.Any<Phone>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Without_any_contact_the_domain_should_refuse()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RegisterCustomerCommand("Ana Souza", "  ", ""), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Customer.ContactRequired");
        _customers.DidNotReceive().Add(Arg.Any<Customer>());
    }

    [Fact]
    public async Task An_email_already_in_the_company_should_be_a_conflict()
    {
        _customers.FindByEmailAsync(_companyId, Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(CustomerData.NewCustomer(_companyId));

        var result = await Handler().HandleAsync(
            new RegisterCustomerCommand("Ana Souza", CustomerData.ValidEmail, CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.EmailAlreadyRegistered");
        _customers.DidNotReceive().Add(Arg.Any<Customer>());
    }

    [Fact]
    public async Task A_phone_already_in_the_company_should_be_a_conflict()
    {
        _customers.FindByPhoneAsync(_companyId, Arg.Any<Phone>(), Arg.Any<CancellationToken>())
            .Returns(CustomerData.NewCustomer(_companyId));

        var result = await Handler().HandleAsync(
            new RegisterCustomerCommand("Ana Souza", CustomerData.ValidEmail, CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.PhoneAlreadyRegistered");
        _customers.DidNotReceive().Add(Arg.Any<Customer>());
    }

    [Fact]
    public async Task The_email_should_be_looked_up_normalized_and_inside_the_company()
    {
        await Handler().HandleAsync(
            new RegisterCustomerCommand(Email: " ANA@Example.COM "), TestContext.Current.CancellationToken);

        await _customers.Received(1).FindByEmailAsync(
            _companyId, Arg.Is<Email>(email => email.Value == "ana@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RegisterCustomerCommand(new string('x', 151), CustomerData.ValidEmail),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Customer.NameInvalid");
        _customers.DidNotReceive().Add(Arg.Any<Customer>());
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var handler = new RegisterCustomerHandler(_customers, Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RegisterCustomerCommand("Ana Souza", CustomerData.ValidEmail), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterCustomerValidator(),
            new RegisterCustomerCommand("Ana Souza", CustomerData.ValidEmail, CustomerData.ValidPhone, true, "pt-BR"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Optional_values_left_blank_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterCustomerValidator(), new RegisterCustomerCommand(Email: "  ", Phone: "", PreferredLanguage: null));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Values_the_domain_refuses_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new RegisterCustomerValidator(),
            new RegisterCustomerCommand("Ana Souza", "not-an-email", "123", false, "fr"));

        problems.ShouldBe(
            ["Email:Validation.Invalid", "Phone:Validation.Invalid", "PreferredLanguage:Validation.Invalid"],
            ignoreOrder: true);
    }

    private RegisterCustomerHandler Handler() => new(_customers, BranchData.TenantOf(_companyId));

    private Func<Customer?> CaptureAdded()
    {
        Customer? added = null;
        _customers.When(repository => repository.Add(Arg.Any<Customer>()))
            .Do(call => added = call.Arg<Customer>());

        return () => added;
    }
}