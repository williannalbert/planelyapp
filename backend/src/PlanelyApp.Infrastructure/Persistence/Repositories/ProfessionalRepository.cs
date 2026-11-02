using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class ProfessionalRepository(PlanelyDbContext context)
    : Repository<Professional>(context), IProfessionalRepository
{
    public Task<Professional?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Context.Set<Professional>().FirstOrDefaultAsync(
            professional => professional.UserId == userId,
            cancellationToken);
}