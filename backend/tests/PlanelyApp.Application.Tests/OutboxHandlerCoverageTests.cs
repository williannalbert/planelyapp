using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests;

public class OutboxHandlerCoverageTests
{
    [Theory]
    [InlineData(typeof(CustomerRegistered))]
    [InlineData(typeof(PromotionCreated))]
    [InlineData(typeof(AppointmentCreated))]
    public void Every_domain_event_of_the_module_should_have_an_outbox_handler(Type anchor)
    {
        var events = anchor.Assembly.GetTypes()
            .Where(type => type.Namespace == anchor.Namespace
                && !type.IsAbstract
                && typeof(IDomainEvent).IsAssignableFrom(type));

        var handled = typeof(AssemblyReference).Assembly.GetTypes()
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToList();

        // A new event without a message is a decision someone has to make on purpose.
        events.Except(handled).ShouldBeEmpty();
    }
}