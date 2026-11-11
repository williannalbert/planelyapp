using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Imports.List;

/// <summary>One page of the imports of the company of the request, the most recent first.</summary>
public sealed record ListImportsQuery(int Page = 1, int PageSize = PageLimits.DefaultPageSize)
    : IQuery<PagedResult<DataImportSummaryDto>>;