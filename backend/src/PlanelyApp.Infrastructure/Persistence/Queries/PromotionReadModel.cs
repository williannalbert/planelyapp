using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class PromotionReadModel(PlanelyDbContext context) : IPromotionReadModel
{
    public async Task<PagedResult<PromotionSummaryDto>> ListAsync(
        string? search,
        PromotionKind? kind,
        bool? isActive,
        DateOnly? validOn,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var promotions = context.Set<Promotion>().AsNoTracking();

        if (search is not null)
        {
            promotions = promotions.Where(promotion => promotion.Name.Contains(search));
        }

        if (kind is { } wantedKind)
        {
            promotions = promotions.Where(promotion => promotion.Kind == wantedKind);
        }

        if (isActive is { } active)
        {
            promotions = promotions.Where(promotion => promotion.IsActive == active);
        }

        if (validOn is { } day)
        {
            promotions = promotions.Where(promotion => promotion.Validity.Start <= day && promotion.Validity.End >= day);
        }

        var totalCount = await promotions.CountAsync(cancellationToken);

        var rows = await promotions
            .OrderByDescending(promotion => promotion.Validity.Start)
            .ThenBy(promotion => promotion.Name)
            .ThenBy(promotion => promotion.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(promotion => new
            {
                promotion.Id,
                promotion.Kind,
                promotion.Name,
                DiscountType = promotion.Discount.Type,
                DiscountValue = promotion.Discount.Value,
                ValidFrom = promotion.Validity.Start,
                ValidUntil = promotion.Validity.End,
                promotion.AppliesToAllBranches,
                // The ids live in a json column read through a converter, so SQL cannot count them: they come with the page.
                ServiceIds = EF.Property<List<Guid>>(promotion, "_serviceIds"),
                promotion.IsActive,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new PromotionSummaryDto(
                row.Id,
                row.Kind,
                row.Name,
                row.DiscountType,
                row.DiscountValue,
                row.ValidFrom,
                row.ValidUntil,
                row.AppliesToAllBranches,
                row.ServiceIds.Count,
                row.IsActive))
            .ToList();

        return new PagedResult<PromotionSummaryDto>(items, page, pageSize, totalCount);
    }
}