using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceCategories;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class ServiceCategoryReadModel(PlanelyDbContext context) : IServiceCategoryReadModel
{
    public async Task<IReadOnlyList<ServiceCategoryDto>> ListAsync(CancellationToken cancellationToken) =>
        await context.Set<ServiceCategory>()
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new ServiceCategoryDto(category.Id, category.Name))
            .ToListAsync(cancellationToken);
}