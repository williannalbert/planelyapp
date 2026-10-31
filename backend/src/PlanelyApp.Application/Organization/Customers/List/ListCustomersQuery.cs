using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Organization.Customers.List;

/// <summary>
/// One page of the customers of the company of the request. <see cref="Search"/> finds the name by a piece of it
/// and the e-mail or the phone (E.164, like +5511999990000) by the whole value.
/// </summary>
public sealed record ListCustomersQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = PageLimits.DefaultPageSize) : IQuery<PagedResult<CustomerSummaryDto>>;