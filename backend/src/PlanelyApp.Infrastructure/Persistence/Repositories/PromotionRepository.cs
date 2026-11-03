using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class PromotionRepository(PlanelyDbContext context)
    : Repository<Promotion>(context), IPromotionRepository
{
    public async Task<IReadOnlyList<Promotion>> ListInForceAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default) =>
        await Context.Set<Promotion>()
            .Where(promotion => promotion.IsActive
                                && promotion.Validity.Start <= localDate
                                && promotion.Validity.End >= localDate)
            .ToListAsync(cancellationToken);
}