using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.Infrastructure.Persistence.Conventions;
using PlanelyApp.Infrastructure.Persistence.Converters;

using System.Reflection;

namespace PlanelyApp.Infrastructure.Persistence;

/// <summary>
/// The only DbContext of the system. It is also the unit of work: SaveChangesAsync is the transaction.
/// Not sealed only so integration tests can derive a context with throwaway entities and exercise the conventions.
/// There are no DbSet properties on purpose: EF would name the tables after them. Repositories use Set&lt;T&gt;().
/// </summary>
public class PlanelyDbContext : DbContext, IUnitOfWork, IDomainEventSource
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

    /// <summary>
    /// Hands over, once, the events raised by every aggregate this context is tracking (new ones and loaded ones),
    /// oldest first. The aggregates forget them, so a second call only returns what was raised since.
    /// </summary>
    public IReadOnlyList<IDomainEvent> TakeDomainEvents()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        // OrderBy is stable: events with the same instant keep the order they were raised in.
        var events = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .OrderBy(domainEvent => domainEvent.OccurredAtUtc)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return events;
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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every property of these types, in any entity, is stored the same way.
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>().HaveMaxLength(254);
        configurationBuilder.Properties<Phone>().HaveConversion<PhoneConverter>().HaveMaxLength(16);
        configurationBuilder.Properties<Cnpj>().HaveConversion<CnpjConverter>().HaveMaxLength(14).AreFixedLength();
        configurationBuilder.Properties<Language>().HaveConversion<LanguageConverter>().HaveMaxLength(10);
        configurationBuilder.Properties<Cpf>().HaveConversion<CpfConverter>().HaveMaxLength(11).AreFixedLength();
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>().HavePrecision(12, 2);
    }
}