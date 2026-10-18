namespace PlanelyApp.Domain.SeedWork;

public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}