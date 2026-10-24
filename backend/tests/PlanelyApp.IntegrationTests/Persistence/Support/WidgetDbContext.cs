using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;

namespace PlanelyApp.IntegrationTests.Persistence.Support;

/// <summary>
/// The real context with only the throwaway entities. It does not call the base ConfigureEntities on purpose,
/// so the real mappings (3.4 onwards) never end up in the shared test database through this context.
/// </summary>
public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options, ITenantProvider tenantProvider)
    : PlanelyDbContext(options, tenantProvider)
{
    protected override void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(widget =>
        {
            widget.HasMany(w => w.Parts).WithOne().HasForeignKey("WidgetId").IsRequired();
            widget.Navigation(w => w.Parts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<WidgetPart>();
    }
}