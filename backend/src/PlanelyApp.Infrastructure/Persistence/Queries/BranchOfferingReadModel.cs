using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Catalog.BranchOfferings;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class BranchOfferingReadModel(PlanelyDbContext context) : IBranchOfferingReadModel
{
    public async Task<PagedResult<BranchOfferingDto>?> ListAsync(
        Guid branchId,
        string? search,
        Guid? categoryId,
        bool? isAvailable,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // The branch stores only where it differs from the company default, so it is a short list.
        var branch = await context.Set<Branch>()
            .AsNoTracking()
            .Where(existing => existing.Id == branchId)
            .Select(existing => new
            {
                Overrides = existing.OfferingOverrides
                    .Select(custom => new { custom.ServiceOfferingId, custom.CustomPrice, custom.IsAvailable })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (branch is null)
        {
            return null;
        }

        var customPrices = branch.Overrides
            .Where(custom => custom.CustomPrice is not null)
            .ToDictionary(custom => custom.ServiceOfferingId, custom => custom.CustomPrice!.Amount);

        var unavailable = branch.Overrides
            .Where(custom => !custom.IsAvailable)
            .Select(custom => custom.ServiceOfferingId)
            .ToList();

        // Only active services can be offered, so only they are listed.
        var services = context.Set<ServiceOffering>().AsNoTracking().Where(service => service.IsActive);

        if (search is not null)
        {
            services = services.Where(service => service.Name.Contains(search));
        }

        if (categoryId is { } category)
        {
            services = services.Where(service => service.CategoryId == category);
        }

        if (isAvailable is { } available)
        {
            services = available
                ? services.Where(service => !unavailable.Contains(service.Id))
                : services.Where(service => unavailable.Contains(service.Id));
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
            })
            .ToListAsync(cancellationToken);

        var categoryNames = await CategoryNames.LoadAsync(
            context, rows.Select(row => row.CategoryId), cancellationToken);

        var items = rows
            .Select(row =>
            {
                var isFixed = row.PricingType == PricingType.Fixed;

                // A service on request ignores any price the branch gave it when it was fixed price (PriceCalculator).
                decimal? customPrice = isFixed && customPrices.TryGetValue(row.Id, out var own) ? own : null;

                return new BranchOfferingDto(
                    row.Id,
                    row.Name,
                    row.CategoryId,
                    row.CategoryId is { } id && categoryNames.TryGetValue(id, out var name) ? name : null,
                    (int)row.StandardDuration.TotalMinutes,
                    row.PricingType,
                    row.BasePrice?.Amount,
                    customPrice,
                    isFixed ? customPrice ?? row.BasePrice?.Amount : null,
                    !unavailable.Contains(row.Id));
            })
            .ToList();

        return new PagedResult<BranchOfferingDto>(items, page, pageSize, totalCount);
    }
}