using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class ServiceOfferingRepository(PlanelyDbContext context)
    : Repository<ServiceOffering>(context), IServiceOfferingRepository
{
    public Task<ServiceOffering?> FindByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        // The Domain stores names trimmed.
        var trimmed = name.Trim();

        return Context.Set<ServiceOffering>().FirstOrDefaultAsync(
            service => service.CompanyId == companyId && service.Name == trimmed,
            cancellationToken);
    }
}