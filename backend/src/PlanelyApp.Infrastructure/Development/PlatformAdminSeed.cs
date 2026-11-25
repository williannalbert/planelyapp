using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Development;

/// <summary>Creates the platform administrator if no user has its identity yet. Running it again changes nothing.</summary>
internal sealed class PlatformAdminSeed(IUserRepository users, IUnitOfWork unitOfWork)
{
    /// <returns>Whether it created the user.</returns>
    public async Task<bool> EnsureAsync(DevelopmentSeedOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (await users.GetByIdentityIdAsync(options.IdentityId, cancellationToken) is not null)
        {
            return false;
        }

        users.Add(User.CreatePlatformAdmin(options.IdentityId, options.Name, Email.Create(options.Email)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}