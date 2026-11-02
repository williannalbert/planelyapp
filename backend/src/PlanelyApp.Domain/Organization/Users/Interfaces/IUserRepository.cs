using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken cancellationToken = default);

    // A user has no tenant filter, so a use case must say which company it is looking in. It never uses the
    // generic GetByIdAsync, which would find a user of any company.
    Task<User?> GetByIdInCompanyAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);

    Task<User?> FindByEmailAsync(Guid companyId, Email email, CancellationToken cancellationToken = default);

    /// <summary>How many active users with the company administrator role the company has.</summary>
    Task<int> CountActiveAdministratorsAsync(Guid companyId, CancellationToken cancellationToken = default);
}