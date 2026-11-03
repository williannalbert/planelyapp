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
    public async Task<IReadOnlyList<Professional>> ListActiveAtBranchAsync(
    Guid branchId,
    CancellationToken cancellationToken = default) =>
    await Context.Set<Professional>()
        .Where(professional => professional.IsActive
                               && professional.WorkingHours.Any(hours => hours.BranchId == branchId))
        .OrderBy(professional => professional.Name)
        .ThenBy(professional => professional.Id)
        .ToListAsync(cancellationToken);
}