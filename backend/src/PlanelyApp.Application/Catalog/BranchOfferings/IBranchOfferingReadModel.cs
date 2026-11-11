using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.BranchOfferings;

/// <summary>Reads the services of a branch for lists, straight from the data store. The company of the request applies.</summary>
public interface IBranchOfferingReadModel
{
    /// <summary>
    /// One page of the active services ordered by name, as the branch has them. With a <paramref name="search"/>
    /// (already trimmed), those whose name contains it. Null when the company has no such branch.
    /// </summary>
    Task<PagedResult<BranchOfferingDto>?> ListAsync(
        Guid branchId,
        string? search,
        Guid? categoryId,
        bool? isAvailable,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}