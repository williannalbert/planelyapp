using NSubstitute;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Organization.Customers.Events;
using PlanelyApp.Domain.Organization.Customers;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class CustomerOutboxHandlersTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly DateTime Occurred = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_registered_customer_should_leave_a_message_with_only_the_id()
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new CustomerRegistered(CustomerId, CompanyId) { OccurredAtUtc = Occurred };

        await new CustomerRegisteredOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new CustomerRegisteredMessage(CustomerId);
        outbox.Received(1).Add("customer.registered", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Fact]
    public async Task An_updated_customer_should_leave_a_message_with_only_the_id()
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new CustomerUpdated(CustomerId, CompanyId) { OccurredAtUtc = Occurred };

        await new CustomerUpdatedOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new CustomerUpdatedMessage(CustomerId);
        outbox.Received(1).Add("customer.updated", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }
}