using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.IntegrationTests.Persistence.Support;

/// <summary>
/// Services for the convention tests: the same registration as production, but for WidgetDbContext and with a fake clock.
/// </summary>
internal sealed class WidgetHost : IAsyncDisposable
{
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);
    private static bool _schemaCreated;

    private readonly ServiceProvider _provider;

    private WidgetHost(string connectionString)
    {
        Clock = new FakeClock(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddPlanelyDbContext<WidgetDbContext>(connectionString)
            .BuildServiceProvider();
    }

    public FakeClock Clock { get; }

    public static async Task<WidgetHost> CreateAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        var host = new WidgetHost(mysql.ConnectionString);
        await host.EnsureSchemaAsync(cancellationToken);
        return host;
    }

    /// <summary>A scope like one request: its own context and its own tenant. Without a company, the tenant stays unset.</summary>
    public AsyncServiceScope CreateScope(Guid? companyId = null)
    {
        var scope = _provider.CreateAsyncScope();

        if (companyId is { } id)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(id);
        }

        return scope;
    }

    public async Task<Guid> SeedWidgetAsync(
        Guid companyId,
        CancellationToken cancellationToken,
        string name = "Widget",
        WidgetStatus status = WidgetStatus.Draft,
        DateTime? scheduledAtUtc = null)
    {
        await using var scope = CreateScope(companyId);
        var context = scope.Db();

        var widget = new Widget(companyId, name, status, scheduledAtUtc ?? Clock.GetUtcNow().UtcDateTime);
        context.Add(widget);
        await context.SaveChangesAsync(cancellationToken);

        return widget.Id;
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    // CreateTablesAsync, not EnsureCreated: EnsureCreated does nothing when the database already has any table.
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await SchemaGate.WaitAsync(cancellationToken);

        try
        {
            if (_schemaCreated)
            {
                return;
            }

            await using var scope = CreateScope();
            var creator = scope.Db().GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync(cancellationToken);

            _schemaCreated = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }
}

internal static class WidgetScopeExtensions
{
    public static WidgetDbContext Db(this AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
}