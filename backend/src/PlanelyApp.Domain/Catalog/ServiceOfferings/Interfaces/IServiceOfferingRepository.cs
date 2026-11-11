using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.ServiceOfferings;

public interface IServiceOfferingRepository : IRepository<ServiceOffering>
{
    // The name is unique inside a company. The spreadsheet import uses this to update existing services.
    Task<ServiceOffering?> FindByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    /// <summary>
    /// The services with these ids that exist in the company of the request (the tenant filter hides the others).
    /// An id that is not found is just missing from the result; the caller compares the counts. Pass distinct ids.
    /// </summary>
    Task<IReadOnlyList<ServiceOffering>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}