using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class BranchRepository(PlanelyDbContext context)
    : Repository<Branch>(context), IBranchRepository
{
    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? exceptBranchId = null,
        CancellationToken cancellationToken = default)
    {
        var branches = Context.Set<Branch>().Where(branch => branch.Name == name);

        if (exceptBranchId is { } id)
        {
            branches = branches.Where(branch => branch.Id != id);
        }

        return branches.AnyAsync(cancellationToken);
    }
}