namespace PlanelyApp.Domain.Catalog.ServiceOfferings;

public interface IServiceOfferingRepository
{
    Task<ServiceOffering?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // The name is unique inside a company. The spreadsheet import uses this to update existing services.
    Task<ServiceOffering?> FindByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);

    void Add(ServiceOffering serviceOffering);
}