namespace PlanelyApp.Domain.SeedWork;

public interface IDomainEvent
{
    Guid Id { get; }

    DateTime OccurredAtUtc { get; }
}