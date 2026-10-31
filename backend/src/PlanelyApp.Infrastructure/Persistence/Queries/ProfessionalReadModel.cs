using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Organization.Professionals;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class ProfessionalReadModel(PlanelyDbContext context) : IProfessionalReadModel
{
    public async Task<IReadOnlyList<ProfessionalSummaryDto>> ListAsync(
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var professionals = context.Set<Professional>().AsNoTracking();

        if (isActive is { } active)
        {
            professionals = professionals.Where(professional => professional.IsActive == active);
        }

        return await professionals
            .OrderBy(professional => professional.Name)
            .Select(professional => new ProfessionalSummaryDto(
                professional.Id, professional.Name, professional.Specialty, professional.IsActive))
            .ToListAsync(cancellationToken);
    }
}