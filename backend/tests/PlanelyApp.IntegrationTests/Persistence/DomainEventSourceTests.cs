using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class DomainEventSourceTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_new_aggregate_should_hand_over_its_events_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await using var scope = mysql.CreateScope(company.Id);
        var customer = StaffData.NewCustomer(company.Id);
        scope.ServiceProvider.GetRequiredService<ICustomerRepository>().Add(customer);
        var source = scope.ServiceProvider.GetRequiredService<IDomainEventSource>();

        var first = source.TakeDomainEvents();
        var second = source.TakeDomainEvents();

        first.ShouldHaveSingleItem().ShouldBeOfType<CustomerRegistered>().CustomerId.ShouldBe(customer.Id);
        second.ShouldBeEmpty();
        customer.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_loaded_aggregate_should_hand_over_what_it_raised_after_being_loaded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var customer = StaffData.NewCustomer(company.Id);
        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(customer));

        await using var scope = mysql.CreateScope(company.Id);
        var loaded = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .GetByIdAsync(customer.Id, cancellationToken);
        var source = scope.ServiceProvider.GetRequiredService<IDomainEventSource>();

        source.TakeDomainEvents().ShouldBeEmpty(); // loading raises nothing

        loaded!.ChangeMarketingConsent(false);

        source.TakeDomainEvents().ShouldHaveSingleItem().ShouldBeOfType<CustomerUpdated>();
    }

    [Fact]
    public async Task Events_should_come_out_in_the_order_they_happened_not_the_order_they_were_tracked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await using var scope = mysql.CreateScope(company.Id);
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var source = scope.ServiceProvider.GetRequiredService<IDomainEventSource>();

        var first = StaffData.NewCustomer(company.Id);
        await Task.Delay(10, cancellationToken);
        var second = StaffData.NewCustomer(company.Id);

        customers.Add(second); // tracked first, but it happened last
        customers.Add(first);

        source.TakeDomainEvents()
            .Cast<CustomerRegistered>()
            .Select(domainEvent => domainEvent.CustomerId)
            .ShouldBe([first.Id, second.Id], ignoreOrder: false);
    }
}