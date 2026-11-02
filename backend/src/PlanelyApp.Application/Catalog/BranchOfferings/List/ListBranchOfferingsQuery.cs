using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.BranchOfferings.List;

/// <summary>
/// One page of the active services of the company as the branch has them. <see cref="Search"/> finds the name by a
/// piece of it; <see cref="CategoryId"/> and <see cref="IsAvailable"/> narrow the list.
/// </summary>
public sealed record ListBranchOfferingsQuery(
    Guid BranchId,
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsAvailable = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<BranchOfferingDto>>;