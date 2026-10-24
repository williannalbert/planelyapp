using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Infrastructure.Persistence.Interceptors;

/// <summary>Fills created_at and updated_at of aggregate roots, using the injected clock so tests can control time.</summary>
public sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries<AggregateRoot>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(AuditProperties.CreatedAt).CurrentValue = now;
                    entry.Property(AuditProperties.UpdatedAt).CurrentValue = now;
                    break;

                case EntityState.Modified:
                    entry.Property(AuditProperties.CreatedAt).IsModified = false;
                    entry.Property(AuditProperties.UpdatedAt).CurrentValue = now;
                    break;
            }
        }
    }
}