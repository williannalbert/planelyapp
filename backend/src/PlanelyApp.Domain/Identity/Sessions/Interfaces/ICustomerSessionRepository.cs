using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Identity.Sessions;

public interface ICustomerSessionRepository : IRepository<CustomerSession>
{
    /// <summary>Finds the session by the hash of the refresh token (see <see cref="CustomerSession.HashToken"/>).</summary>
    Task<CustomerSession?> GetByRefreshTokenHashAsync(
        Guid companyId,
        string refreshTokenHash,
        CancellationToken cancellationToken = default);
}