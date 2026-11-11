using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.UpdateContact;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class UpdateCustomerContactTests
{
    [Fact]
    public async Task The_contact_should_be_replaced_and_the_customer_marked_as_updated()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new UpdateCustomerContactHandler(CustomerData.RepositoryWith(customer));

        var result = await handler.HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, "Bruna@Example.com", "+5511977776666"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.Email!.Value.ShouldBe("bruna@example.com");
        customer.Phone!.Value.ShouldBe("+5511977776666");
        customer.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CustomerUpdated>();
    }

    [Fact]
    public async Task Keeping_the_own_contact_should_not_be_a_conflict_nor_an_update()
    {
        var customer = CustomerData.NewCustomer();
        var customers = CustomerData.RepositoryWith(customer);
        customers.FindByEmailAsync(customer.CompanyId, Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(customer);
        customers.FindByPhoneAsync(customer.CompanyId, Arg.Any<Phone>(), Arg.Any<CancellationToken>()).Returns(customer);

        var result = await new UpdateCustomerContactHandler(customers).HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, CustomerData.ValidEmail, CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_email_of_another_customer_should_be_a_conflict_and_change_nothing()
    {
        var customer = CustomerData.NewCustomer();
        var customers = CustomerData.RepositoryWith(customer);
        customers.FindByEmailAsync(customer.CompanyId, Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(CustomerData.NewCustomer(customer.CompanyId));

        var result = await new UpdateCustomerContactHandler(customers).HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, "other@example.com", CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.EmailAlreadyRegistered");
        customer.Email!.Value.ShouldBe(CustomerData.ValidEmail);
    }

    [Fact]
    public async Task A_phone_of_another_customer_should_be_a_conflict_and_change_nothing()
    {
        var customer = CustomerData.NewCustomer();
        var customers = CustomerData.RepositoryWith(customer);
        customers.FindByPhoneAsync(customer.CompanyId, Arg.Any<Phone>(), Arg.Any<CancellationToken>())
            .Returns(CustomerData.NewCustomer(customer.CompanyId));

        var result = await new UpdateCustomerContactHandler(customers).HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, CustomerData.ValidEmail, "+5511977776666"),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.PhoneAlreadyRegistered");
        customer.Phone!.Value.ShouldBe(CustomerData.ValidPhone);
    }

    [Fact]
    public async Task A_blank_email_should_remove_it_while_the_phone_remains()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new UpdateCustomerContactHandler(CustomerData.RepositoryWith(customer));

        var result = await handler.HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, "  ", CustomerData.ValidPhone),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.Email.ShouldBeNull();
        customer.Phone!.Value.ShouldBe(CustomerData.ValidPhone);
    }

    [Fact]
    public async Task Removing_both_contacts_should_be_refused_by_the_domain()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new UpdateCustomerContactHandler(CustomerData.RepositoryWith(customer));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new UpdateCustomerContactCommand(customer.Id, " ", null), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Customer.ContactRequired");
        customer.Email!.Value.ShouldBe(CustomerData.ValidEmail);
        customer.Phone!.Value.ShouldBe(CustomerData.ValidPhone);
    }

    [Fact]
    public async Task A_missing_customer_should_be_not_found()
    {
        var customers = CustomerData.RepositoryWith(null);

        var result = await new UpdateCustomerContactHandler(customers).HandleAsync(
            new UpdateCustomerContactCommand(Guid.NewGuid(), CustomerData.ValidEmail, null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
        await customers.DidNotReceive().FindByEmailAsync(
            Arg.Any<Guid>(), Arg.Any<Email>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateCustomerContactValidator(),
            new UpdateCustomerContactCommand(Guid.NewGuid(), CustomerData.ValidEmail, null));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Values_the_domain_refuses_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateCustomerContactValidator(),
            new UpdateCustomerContactCommand(Guid.NewGuid(), "not-an-email", "123"));

        problems.ShouldBe(["Email:Validation.Invalid", "Phone:Validation.Invalid"], ignoreOrder: true);
    }
}