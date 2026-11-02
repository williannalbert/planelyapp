namespace PlanelyApp.Application.Catalog.ServiceCategories;

/// <summary>Reads categories for lists, straight from the data store. The company of the request applies.</summary>
public interface IServiceCategoryReadModel
{
    /// <summary>The categories of the company ordered by name.</summary>
    Task<IReadOnlyList<ServiceCategoryDto>> ListAsync(CancellationToken cancellationToken);
}