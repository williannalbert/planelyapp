using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.List;

/// <summary>
/// One page of the services of the company of the request. <see cref="Search"/> finds the name by a piece of it;
/// <see cref="CategoryId"/> and <see cref="IsActive"/> narrow the list.
/// </summary>
public sealed record ListServiceOfferingsQuery(
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<ServiceOfferingSummaryDto>>;