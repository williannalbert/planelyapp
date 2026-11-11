using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Imports.List;

internal sealed class ListImportsHandler(IDataImportReadModel readModel)
    : IQueryHandler<ListImportsQuery, PagedResult<DataImportSummaryDto>>
{
    public async Task<Result<PagedResult<DataImportSummaryDto>>> HandleAsync(
        ListImportsQuery query,
        CancellationToken cancellationToken) =>
        Result<PagedResult<DataImportSummaryDto>>.Success(
            await readModel.ListAsync(query.Page, query.PageSize, cancellationToken));
}