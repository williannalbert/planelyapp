using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// What every repository does. Add only tracks the aggregate; the transaction is the unit of work's SaveChangesAsync.
/// </summary>
internal abstract class Repository<TAggregate>(PlanelyDbContext context) : IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    protected PlanelyDbContext Context { get; } = context;

    // A query, not Find: it goes through the pipeline, so the tenant filter applies.
    public Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.Set<TAggregate>().FirstOrDefaultAsync(aggregate => aggregate.Id == id, cancellationToken);

    public void Add(TAggregate aggregate) => Context.Set<TAggregate>().Add(aggregate);
}