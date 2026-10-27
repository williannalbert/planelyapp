using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Identity.Sessions;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class CustomerSessionRepository(PlanelyDbContext context)
    : Repository<CustomerSession>(context), ICustomerSessionRepository
{
    public Task<CustomerSession?> GetByRefreshTokenHashAsync(
        Guid companyId,
        string refreshTokenHash,
        CancellationToken cancellationToken = default) =>
        Context.Set<CustomerSession>().FirstOrDefaultAsync(
            session => session.CompanyId == companyId && session.RefreshTokenHash == refreshTokenHash,
            cancellationToken);
}