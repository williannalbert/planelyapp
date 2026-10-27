using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Identity.AccessCodes;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class AccessCodeRepository(PlanelyDbContext context)
    : Repository<AccessCode>(context), IAccessCodeRepository
{
    public Task<AccessCode?> GetLatestAsync(
        Guid companyId,
        string destination,
        CancellationToken cancellationToken = default) =>
        Context.Set<AccessCode>()
            .Where(code => code.CompanyId == companyId && code.Destination == destination)
            .OrderByDescending(code => code.CreatedAtUtc)
            .ThenByDescending(code => code.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DateTime>> GetCreationTimesSinceAsync(
        Guid companyId,
        string destination,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default) =>
        await Context.Set<AccessCode>()
            .Where(code => code.CompanyId == companyId
                && code.Destination == destination
                && code.CreatedAtUtc >= sinceUtc)
            .Select(code => code.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}