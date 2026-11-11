using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Organization.Customers;

/// <summary>Reads customers for lists, straight from the data store. The company of the request applies.</summary>
public interface ICustomerReadModel
{
    /// <summary>
    /// One page of the customers ordered by name (the unnamed last, the id breaks ties). With a <paramref name="search"/>
    /// (already trimmed), those whose name contains it or whose e-mail or phone is exactly it.
    /// </summary>
    Task<PagedResult<CustomerSummaryDto>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}