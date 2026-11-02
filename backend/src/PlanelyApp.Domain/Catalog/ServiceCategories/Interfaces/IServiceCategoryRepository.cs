using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.ServiceCategories;

public interface IServiceCategoryRepository : IRepository<ServiceCategory>
{
    /// <summary>
    /// True when the company already has a category with this name (names are unique per company and the comparison
    /// ignores case). Pass the id of a category to leave it out, so renaming a category to its own name is not a conflict.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid? exceptCategoryId = null, CancellationToken cancellationToken = default);
}