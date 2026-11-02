using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Professionals;

public interface IProfessionalRepository : IRepository<Professional>
{
    /// <summary>The professional of the company of the request who is linked to this user, if there is one.</summary>
    Task<Professional?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}