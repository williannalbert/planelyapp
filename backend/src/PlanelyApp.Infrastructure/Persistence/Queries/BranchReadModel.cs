using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Organization.Branches;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class BranchReadModel(PlanelyDbContext context) : IBranchReadModel
{
    public async Task<IReadOnlyList<BranchSummaryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var branches = context.Set<Branch>().AsNoTracking();

        if (isActive is { } active)
        {
            branches = branches.Where(branch => branch.IsActive == active);
        }

        var rows = await branches
            .OrderBy(branch => branch.Name)
            .Select(branch => new
            {
                branch.Id,
                branch.Name,
                branch.Address.City,
                branch.Address.State,
                branch.Phone,
                branch.IsActive,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new BranchSummaryDto(row.Id, row.Name, row.City, row.State, row.Phone.Value, row.IsActive))
            .ToList();
    }
}