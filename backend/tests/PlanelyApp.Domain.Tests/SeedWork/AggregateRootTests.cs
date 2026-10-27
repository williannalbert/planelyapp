using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SeedWork;

public class AggregateRootTests
{
    [Fact]
    public void New_aggregate_should_have_no_domain_events()
    {
        new FakeAggregate().DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Raised_events_should_be_exposed_in_order()
    {
        var aggregate = new FakeAggregate();

        aggregate.Raise();
        aggregate.Raise();

        aggregate.DomainEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void Clearing_should_remove_all_events()
    {
        var aggregate = new FakeAggregate();
        aggregate.Raise();

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_event_should_have_an_id_and_a_utc_timestamp()
    {
        var domainEvent = new FakeEvent();

        domainEvent.Id.ShouldNotBe(Guid.Empty);
        domainEvent.OccurredAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    private sealed record FakeEvent : DomainEvent;

    private sealed class FakeAggregate : AggregateRoot
    {
        public void Raise() => AddDomainEvent(new FakeEvent());
    }
}