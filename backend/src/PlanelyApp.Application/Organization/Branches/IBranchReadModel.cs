namespace PlanelyApp.Application.Organization.Branches;

/// <summary>
/// Reads branches for lists, straight from the data store and without loading aggregates.
/// Implemented by the Infrastructure; the company of the request applies.
/// </summary>
public interface IBranchReadModel
{
    /// <summary>The branches of the company ordered by name. With a value in <paramref name="isActive"/>, only those.</summary>
    Task<IReadOnlyList<BranchSummaryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken);
}