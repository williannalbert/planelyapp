using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Branches;

public interface IBranchRepository : IRepository<Branch>
{
    /// <summary>
    /// True when the company already has a branch with this name (names are unique per company and the comparison
    /// ignores case). Pass the id of a branch to leave it out, so renaming a branch to its own name is not a conflict.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid? exceptBranchId = null, CancellationToken cancellationToken = default);
}