using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.ServiceOfferings;

/// <summary>Reads services for lists, straight from the data store. The company of the request applies.</summary>
public interface IServiceOfferingReadModel
{
    /// <summary>
    /// One page of the services ordered by name (the id breaks ties). With a <paramref name="search"/> (already trimmed),
    /// those whose name contains it; with a category or a status, only those.
    /// </summary>
    Task<PagedResult<ServiceOfferingSummaryDto>> ListAsync(
        string? search,
        Guid? categoryId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}