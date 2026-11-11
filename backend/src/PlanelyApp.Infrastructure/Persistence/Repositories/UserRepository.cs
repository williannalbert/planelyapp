using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(PlanelyDbContext context)
    : Repository<User>(context), IUserRepository
{
    // Users have no tenant filter on purpose: this runs before the company of the request is known.
    public Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken cancellationToken = default) =>
        Context.Set<User>().FirstOrDefaultAsync(user => user.IdentityId == identityId, cancellationToken);

    public Task<User?> GetByIdInCompanyAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default) =>
        Context.Set<User>().FirstOrDefaultAsync(
            user => user.CompanyId == companyId && user.Id == id,
            cancellationToken);

    public Task<User?> FindByEmailAsync(Guid companyId, Email email, CancellationToken cancellationToken = default) =>
        Context.Set<User>().FirstOrDefaultAsync(
            user => user.CompanyId == companyId && user.Email == email,
            cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        Context.Set<User>().CountAsync(
            user => user.CompanyId == companyId && user.Role == UserRole.CompanyAdmin && user.IsActive,
            cancellationToken);
}