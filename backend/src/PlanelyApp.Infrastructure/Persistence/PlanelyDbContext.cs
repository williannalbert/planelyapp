using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence.Conventions;

namespace PlanelyApp.Infrastructure.Persistence;

/// <summary>
/// The only DbContext of the system. It is also the unit of work: SaveChangesAsync is the transaction.
/// Not sealed only so integration tests can derive a context with throwaway entities and exercise the conventions.
/// There are no DbSet properties on purpose: EF would name the tables after them. Repositories use Set&lt;T&gt;().
/// </summary>
public class PlanelyDbContext : DbContext, IUnitOfWork
{
    private static readonly MethodInfo TenantFilterMethod = typeof(PlanelyDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantProvider _tenantProvider;

    public PlanelyDbContext(DbContextOptions<PlanelyDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected PlanelyDbContext(DbContextOptions options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    // Read by the query filter on every query, so it follows the scope, not the moment the model was built.
    // No company means Guid.Empty, which no row has: the filter fails closed.
    private Guid CurrentCompanyId => _tenantProvider.CompanyId ?? Guid.Empty;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEntities(modelBuilder);
        modelBuilder.ApplyPlanelyConventions();
        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>Where the entities are mapped. Derived contexts (tests) replace it with their own entities.</summary>
    protected virtual void ConfigureEntities(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssemblyReference).Assembly);

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var tenantEntities = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => entityType.BaseType is null
                && !entityType.IsOwned()
                && typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var clrType in tenantEntities)
        {
            TenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.CompanyId == CurrentCompanyId);
}