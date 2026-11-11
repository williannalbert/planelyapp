using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.UpdateProfile;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class UpdateCustomerProfileTests
{
    [Fact]
    public async Task The_profile_should_be_replaced_and_the_cpf_checked_in_the_company()
    {
        var customer = CustomerData.NewCustomer();
        var customers = CustomerData.RepositoryWith(customer);

        var result = await new UpdateCustomerProfileHandler(customers).HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, "  Ana Maria  ", "529.982.247-25", new DateOnly(1990, 5, 17), "es"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.Name.ShouldBe("Ana Maria");
        customer.Cpf!.Value.ShouldBe(CustomerData.ValidCpf);
        customer.BirthDate.ShouldBe(new DateOnly(1990, 5, 17));
        customer.PreferredLanguage!.Code.ShouldBe("es");
        customer.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CustomerUpdated>();
        await customers.Received(1).ExistsByCpfAsync(
            customer.CompanyId, Arg.Is<Cpf>(cpf => cpf.Value == CustomerData.ValidCpf), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cpf_of_another_customer_should_be_a_conflict_and_change_nothing()
    {
        var customer = CustomerData.NewCustomer();
        var customers = CustomerData.RepositoryWith(customer);
        customers.ExistsByCpfAsync(customer.CompanyId, Arg.Any<Cpf>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await new UpdateCustomerProfileHandler(customers).HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, "Ana Maria", CustomerData.ValidCpf, null, null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.CpfAlreadyRegistered");
        customer.Name.ShouldBe("Ana Souza");
        customer.Cpf.ShouldBeNull();
    }

    [Fact]
    public async Task Keeping_the_own_cpf_should_not_be_a_conflict_nor_an_update_nor_a_query()
    {
        var customer = CustomerData.NewCustomer();
        customer.UpdateProfile("Ana Souza", Cpf.Create(CustomerData.ValidCpf), null, null);
        customer.ClearDomainEvents();
        var customers = CustomerData.RepositoryWith(customer);
        customers.ExistsByCpfAsync(customer.CompanyId, Arg.Any<Cpf>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await new UpdateCustomerProfileHandler(customers).HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, "Ana Souza", "529.982.247-25", null, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.DomainEvents.ShouldBeEmpty();
        await customers.DidNotReceive().ExistsByCpfAsync(
            Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_cpf_should_remove_it_without_a_query()
    {
        var customer = CustomerData.NewCustomer();
        customer.UpdateProfile("Ana Souza", Cpf.Create(CustomerData.ValidCpf), null, null);
        var customers = CustomerData.RepositoryWith(customer);

        var result = await new UpdateCustomerProfileHandler(customers).HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, "Ana Souza", "  ", null, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.Cpf.ShouldBeNull();
        await customers.DidNotReceive().ExistsByCpfAsync(
            Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fields_left_out_should_be_cleared()
    {
        var customer = CustomerData.NewCustomer();
        customer.UpdateProfile("Ana Souza", Cpf.Create(CustomerData.ValidCpf), new DateOnly(1990, 5, 17), Language.English);
        var handler = new UpdateCustomerProfileHandler(CustomerData.RepositoryWith(customer));

        var result = await handler.HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, null, null, null, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.Name.ShouldBeNull();
        customer.Cpf.ShouldBeNull();
        customer.BirthDate.ShouldBeNull();
        customer.PreferredLanguage.ShouldBeNull();
    }

    [Fact]
    public async Task A_birth_date_before_1900_should_be_refused_by_the_domain()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new UpdateCustomerProfileHandler(CustomerData.RepositoryWith(customer));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, "Ana", null, new DateOnly(1899, 12, 31), null),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Customer.BirthDateInvalid");
        customer.BirthDate.ShouldBeNull();
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new UpdateCustomerProfileHandler(CustomerData.RepositoryWith(customer));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new UpdateCustomerProfileCommand(customer.Id, new string('x', 151), null, null, null),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Customer.NameInvalid");
        customer.Name.ShouldBe("Ana Souza");
    }

    [Fact]
    public async Task A_missing_customer_should_be_not_found()
    {
        var result = await new UpdateCustomerProfileHandler(CustomerData.RepositoryWith(null)).HandleAsync(
            new UpdateCustomerProfileCommand(Guid.NewGuid(), "Ana", null, null, null),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateCustomerProfileValidator(),
            new UpdateCustomerProfileCommand(Guid.NewGuid(), "Ana", "529.982.247-25", new DateOnly(1990, 5, 17), "pt-BR"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Values_the_domain_refuses_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateCustomerProfileValidator(),
            new UpdateCustomerProfileCommand(Guid.NewGuid(), "Ana", "111.111.111-11", null, "fr"));

        problems.ShouldBe(["Cpf:Validation.Invalid", "PreferredLanguage:Validation.Invalid"], ignoreOrder: true);
    }
}