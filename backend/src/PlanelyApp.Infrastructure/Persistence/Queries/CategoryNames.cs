using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Catalog.ServiceCategories;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>
/// The names of categories for the lists that show them. The aggregates have no navigation between them,
/// so the names of the page come in one more query.
/// </summary>
internal static class CategoryNames
{
    public static async Task<Dictionary<Guid, string>> LoadAsync(
        PlanelyDbContext context,
        IEnumerable<Guid?> categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await context.Set<ServiceCategory>()
            .AsNoTracking()
            .Where(category => ids.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
    }
}