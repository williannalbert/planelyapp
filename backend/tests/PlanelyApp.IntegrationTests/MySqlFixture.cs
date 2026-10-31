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
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
    .WithCommand("--innodb-print-all-deadlocks=ON")
    .Build();

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

        await ReportDeadlocksAsync();
        await _container.DisposeAsync();
    }

    // Deadlocks showed up now and then in this suite. When MySQL reports one, print it, so the two statements that
    // collided can be seen (the run shows the standard output when a test fails).
    private async Task ReportDeadlocksAsync()
    {
        try
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            var lines = $"{stdout}\n{stderr}".Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("deadlock detected", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(string.Join('\n', lines.Skip(i).Take(60)));
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Could not read the MySQL log: {exception.Message}");
        }
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