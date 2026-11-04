using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Tenancy;
using PlanelyApp.Application.Abstractions.Identity;
using Testcontainers.MySql;
using System.Text;

[assembly: AssemblyFixture(typeof(PlanelyApp.IntegrationTests.MySqlFixture))]

namespace PlanelyApp.IntegrationTests;

/// <summary>
/// One disposable MySQL, the same version as the docker-compose, shared by every test of this assembly.
/// It starts before the first test, gets the migrations applied, and is removed after the last one.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
       .WithCommand("--innodb-print-all-deadlocks=ON", "--log-error-verbosity=3")
       .Build();

    private ServiceProvider? _services;
    public string ConnectionString => _container.GetConnectionString();
    /// <summary>The identity provider the use cases talk to in these tests (the Keycloak adapter comes in 4.5d).</summary>
    public FakeIdentityProvider Identity { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Registered by the same method the Api uses, so the tests also prove the registration.
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IIdentityProvider>(Identity)
            .AddScoped<FakeCurrentUser>()
            .AddScoped<ICurrentUser>(provider => provider.GetRequiredService<FakeCurrentUser>())
            .AddInfrastructure(ConnectionString)
            .AddApplication()
            .BuildServiceProvider();

        // The same migrations production will run. A stale migration or a wrong mapping fails here, before any test.
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlanelyDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await ReportDeadlocksAsync();

        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    // Deadlocks showed up now and then in this suite. When MySQL reports one, print it, so the two statements that
    // collided can be seen (the run shows the standard output when a test fails).
    private async Task ReportDeadlocksAsync()
    {
        var report = new StringBuilder();

        try
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            report.AppendLine("== MySQL container log ==");
            report.AppendLine(stdout);
            report.AppendLine(stderr);
        }
        catch (Exception exception)
        {
            report.AppendLine($"Could not read the MySQL log: {exception.Message}");
        }

        try
        {
            var status = await _container.ExecAsync(
                ["sh", "-c", "mysql -uroot -p\"$MYSQL_ROOT_PASSWORD\" -e 'SHOW ENGINE INNODB STATUS\\G'"]);
            var start = status.Stdout.IndexOf("LATEST DETECTED DEADLOCK", StringComparison.Ordinal);

            report.AppendLine("== INNODB STATUS ==");
            report.AppendLine(start >= 0
                ? status.Stdout.Substring(start, Math.Min(8000, status.Stdout.Length - start))
                : "No InnoDB deadlock recorded.");
        }
        catch (Exception exception)
        {
            report.AppendLine($"Could not read INNODB STATUS: {exception.Message}");
        }

        var path = Path.Combine(AppContext.BaseDirectory, $"mysql-run-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await File.WriteAllTextAsync(path, report.ToString());
        Console.WriteLine($"MySQL report written to {path}");
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