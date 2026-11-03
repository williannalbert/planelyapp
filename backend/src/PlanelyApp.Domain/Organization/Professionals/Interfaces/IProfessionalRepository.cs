using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Professionals;

public interface IProfessionalRepository : IRepository<Professional>
{
    /// <summary>The professional of the company of the request who is linked to this user, if there is one.</summary>
    Task<Professional?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// The active professionals of the company of the request who have working hours at this branch, by name.
    /// Whether they perform a given service is for the caller to check.
    /// </summary>
    Task<IReadOnlyList<Professional>> ListActiveAtBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);
}