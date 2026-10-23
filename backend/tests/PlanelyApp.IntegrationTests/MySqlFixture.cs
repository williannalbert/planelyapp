using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure;
using Testcontainers.MySql;

[assembly: AssemblyFixture(typeof(PlanelyApp.IntegrationTests.MySqlFixture))]

namespace PlanelyApp.IntegrationTests;

/// <summary>
/// One disposable MySQL, the same version as the docker-compose, shared by every test of this assembly.
/// It starts before the first test and is removed after the last one.
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
            .AddInfrastructure(ConnectionString)
            .BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>A scope like one web request has: its own PlanelyDbContext. Dispose it when done.</summary>
    public AsyncServiceScope CreateScope() => _services!.CreateAsyncScope();
}