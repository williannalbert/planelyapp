namespace PlanelyApp.Domain.Organization.Branches;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Branch branch);
}