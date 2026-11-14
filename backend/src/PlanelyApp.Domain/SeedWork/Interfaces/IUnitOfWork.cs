namespace PlanelyApp.Domain.SeedWork;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets everything that is tracked and was not saved. After a failed save the use case can start again from the
    /// reads, as if the request had just arrived.
    /// </summary>
    void DiscardChanges();
}