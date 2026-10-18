namespace PlanelyApp.Domain.Organization.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken cancellationToken = default);

    void Add(User user);
}