using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions;

/// <summary>Reads promotions for lists, straight from the data store. The company of the request applies.</summary>
public interface IPromotionReadModel
{
    /// <summary>
    /// One page of the promotions, the ones that start latest first (then by name, then by id). With a
    /// <paramref name="search"/> (already trimmed), those whose name contains it; with a kind, a status or a day
    /// (<paramref name="validOn"/>: the validity period contains it), only those.
    /// </summary>
    Task<PagedResult<PromotionSummaryDto>> ListAsync(
        string? search,
        PromotionKind? kind,
        bool? isActive,
        DateOnly? validOn,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}