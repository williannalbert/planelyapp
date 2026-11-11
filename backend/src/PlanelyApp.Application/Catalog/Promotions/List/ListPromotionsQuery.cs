using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.List;

/// <summary>
/// One page of the promotions of the company of the request. <see cref="Search"/> finds the name by a piece of it;
/// <see cref="Kind"/>, <see cref="IsActive"/> and <see cref="ValidOn"/> (a day inside the validity period) narrow the list.
/// </summary>
public sealed record ListPromotionsQuery(
    string? Search = null,
    PromotionKind? Kind = null,
    bool? IsActive = null,
    DateOnly? ValidOn = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<PromotionSummaryDto>>;