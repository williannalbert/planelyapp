using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Imports;

/// <summary>Reads imports for lists, straight from the data store. The company of the request applies.</summary>
public interface IDataImportReadModel
{
    /// <summary>One page of the imports, the most recent first (the id breaks ties).</summary>
    Task<PagedResult<DataImportSummaryDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}