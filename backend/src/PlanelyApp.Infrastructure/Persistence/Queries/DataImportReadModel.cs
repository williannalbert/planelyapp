using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Imports;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class DataImportReadModel(PlanelyDbContext context) : IDataImportReadModel
{
    public async Task<PagedResult<DataImportSummaryDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var imports = context.Set<DataImport>().AsNoTracking();

        var totalCount = await imports.CountAsync(cancellationToken);

        var items = await imports
            .OrderByDescending(import => import.CreatedAtUtc)
            .ThenByDescending(import => import.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(import => new DataImportSummaryDto(
                import.Id,
                import.Type,
                import.FileName,
                import.Status,
                import.TotalRows,
                import.SuccessRows + import.ErrorRows,
                import.ErrorRows,
                import.CreatedAtUtc,
                import.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<DataImportSummaryDto>(items, page, pageSize, totalCount);
    }
}