using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.ServiceOfferings;

public interface IServiceOfferingRepository : IRepository<ServiceOffering>
{
    // The name is unique inside a company. The spreadsheet import uses this to update existing services.
    Task<ServiceOffering?> FindByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
}