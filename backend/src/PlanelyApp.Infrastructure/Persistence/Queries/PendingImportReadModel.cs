using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Imports.Processing;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>
/// Looks across every company on purpose (IgnoreQueryFilters), so it only reads and only returns ids. What is done with
/// each id happens in a scope that has its company.
/// </summary>
internal sealed class PendingImportReadModel(PlanelyDbContext context) : IPendingImportReadModel
{
    public async Task<IReadOnlyList<PendingImport>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        return await context.Set<DataImport>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(import => import.Status == ImportStatus.Pending || import.Status == ImportStatus.Processing)
            .OrderBy(import => import.CreatedAtUtc)
            .ThenBy(import => import.Id)
            .Select(import => new PendingImport(import.Id, import.CompanyId))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}