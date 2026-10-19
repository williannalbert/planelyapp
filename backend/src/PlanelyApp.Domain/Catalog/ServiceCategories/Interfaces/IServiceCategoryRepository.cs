namespace PlanelyApp.Domain.Catalog.ServiceCategories;

public interface IServiceCategoryRepository
{
    Task<ServiceCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ServiceCategory category);
}