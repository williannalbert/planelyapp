using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The query filter protects reads. This protects writes: nothing that belongs to another company
/// (or to no company) is saved, even if the entity got into the context by an unfiltered query or by Attach.
/// </summary>
public sealed class TenantWriteInterceptor(ITenantProvider tenantProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Verify(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Verify(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Verify(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityName = entry.Metadata.ShortName();

            var companyId = tenantProvider.CompanyId
                ?? throw new TenantViolationException($"{entityName} cannot be saved: no company is set for the current scope.");

            if (entry.Entity.CompanyId != companyId)
            {
                throw new TenantViolationException(
                    $"{entityName} belongs to company {entry.Entity.CompanyId}, but the current scope is company {companyId}.");
            }

            // Also the value that was read: a row cannot be moved from one company to another.
            if (entry.State == EntityState.Modified
                && entry.Property(nameof(ITenantEntity.CompanyId)).OriginalValue is Guid original
                && original != companyId)
            {
                throw new TenantViolationException($"{entityName} was read from company {original}, but the current scope is company {companyId}.");
            }
        }
    }
}