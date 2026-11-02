using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class ServiceOfferingReadModel(PlanelyDbContext context) : IServiceOfferingReadModel
{
    public async Task<PagedResult<ServiceOfferingSummaryDto>> ListAsync(
        string? search,
        Guid? categoryId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var services = context.Set<ServiceOffering>().AsNoTracking();

        if (search is not null)
        {
            services = services.Where(service => service.Name.Contains(search));
        }

        if (categoryId is { } category)
        {
            services = services.Where(service => service.CategoryId == category);
        }

        if (isActive is { } active)
        {
            services = services.Where(service => service.IsActive == active);
        }

        var totalCount = await services.CountAsync(cancellationToken);

        var rows = await services
            .OrderBy(service => service.Name)
            .ThenBy(service => service.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(service => new
            {
                service.Id,
                service.Name,
                service.CategoryId,
                service.StandardDuration,
                service.PricingType,
                service.BasePrice,
                service.IsActive,
            })
            .ToListAsync(cancellationToken);

        // The aggregates have no navigation between them, so the names of the categories of this page come in one more query.
        var categoryIds = rows
            .Where(row => row.CategoryId is not null)
            .Select(row => row.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Set<ServiceCategory>()
                .AsNoTracking()
                .Where(existing => categoryIds.Contains(existing.Id))
                .ToDictionaryAsync(existing => existing.Id, existing => existing.Name, cancellationToken);

        var items = rows
            .Select(row => new ServiceOfferingSummaryDto(
                row.Id,
                row.Name,
                row.CategoryId,
                row.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : null,
                (int)row.StandardDuration.TotalMinutes,
                row.PricingType,
                row.BasePrice?.Amount,
                row.IsActive))
            .ToList();

        return new PagedResult<ServiceOfferingSummaryDto>(items, page, pageSize, totalCount);
    }
}