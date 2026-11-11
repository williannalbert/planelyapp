using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;
using PlanelyApp.Domain.Organization.Customers;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class ChangeCustomerMarketingConsentTests
{
    [Fact]
    public async Task Giving_the_consent_should_be_saved_and_mark_the_customer_as_updated()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new ChangeCustomerMarketingConsentHandler(CustomerData.RepositoryWith(customer));

        var result = await handler.HandleAsync(
            new ChangeCustomerMarketingConsentCommand(customer.Id, true), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.AcceptsMarketing.ShouldBeTrue();
        customer.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CustomerUpdated>();
    }

    [Fact]
    public async Task Repeating_the_current_choice_should_change_nothing()
    {
        var customer = CustomerData.NewCustomer();
        var handler = new ChangeCustomerMarketingConsentHandler(CustomerData.RepositoryWith(customer));

        var result = await handler.HandleAsync(
            new ChangeCustomerMarketingConsentCommand(customer.Id, false), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        customer.AcceptsMarketing.ShouldBeFalse();
        customer.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_customer_should_be_not_found()
    {
        var handler = new ChangeCustomerMarketingConsentHandler(CustomerData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new ChangeCustomerMarketingConsentCommand(Guid.NewGuid(), true), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
    }
}