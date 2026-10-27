using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(PlanelyDbContext context)
    : Repository<User>(context), IUserRepository
{
    // Users have no tenant filter on purpose: this runs before the company of the request is known.
    public Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken cancellationToken = default) =>
        Context.Set<User>().FirstOrDefaultAsync(user => user.IdentityId == identityId, cancellationToken);
}