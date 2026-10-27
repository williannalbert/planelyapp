using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken cancellationToken = default);
}