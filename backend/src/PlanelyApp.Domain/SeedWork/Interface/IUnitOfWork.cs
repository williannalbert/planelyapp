namespace PlanelyApp.Domain.SeedWork.Interface;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}