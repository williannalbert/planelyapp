namespace PlanelyApp.Domain.SeedWork;

/// <summary>The operations every aggregate repository has. One repository per aggregate root.</summary>
public interface IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);
}