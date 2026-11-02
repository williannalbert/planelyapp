using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Catalog.ServiceCategories;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class ServiceCategoryRepository(PlanelyDbContext context)
    : Repository<ServiceCategory>(context), IServiceCategoryRepository
{
    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? exceptCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        var categories = Context.Set<ServiceCategory>().Where(category => category.Name == name);

        if (exceptCategoryId is { } id)
        {
            categories = categories.Where(category => category.Id != id);
        }

        return categories.AnyAsync(cancellationToken);
    }
}