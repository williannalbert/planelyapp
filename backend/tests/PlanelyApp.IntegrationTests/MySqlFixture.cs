using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Tenancy;
using Testcontainers.MySql;

[assembly: AssemblyFixture(typeof(PlanelyApp.IntegrationTests.MySqlFixture))]

namespace PlanelyApp.IntegrationTests;

/// <summary>
/// One disposable MySQL, the same version as the docker-compose, shared by every test of this assembly.
/// It starts before the first test, gets the migrations applied, and is removed after the last one.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();

    private ServiceProvider? _services;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Registered by the same method the Api uses, so the tests also prove the registration.
        _services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(ConnectionString)
            .AddApplication()
            .BuildServiceProvider();

        // The same migrations production will run. A stale migration or a wrong mapping fails here, before any test.
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlanelyDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// A scope like one web request has: its own PlanelyDbContext, and the company set when one is given.
    /// Dispose it when done.
    /// </summary>
    public AsyncServiceScope CreateScope(Guid? companyId = null)
    {
        var scope = _services!.CreateAsyncScope();

        if (companyId is { } id)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(id);
        }

        return scope;
    }
}