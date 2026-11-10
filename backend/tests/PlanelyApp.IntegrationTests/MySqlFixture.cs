using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Identity;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Tenancy;
using PlanelyApp.IntegrationTests.Identity;
using System.Text;
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
       .WithCommand("--innodb-print-all-deadlocks=ON", "--log-error-verbosity=3")
       .Build();

    private ServiceProvider? _services;
    public string ConnectionString => _container.GetConnectionString();
    /// <summary>The identity provider the use cases talk to in these tests (the Keycloak adapter comes in 4.5d).</summary>
    public FakeIdentityProvider Identity { get; } = new();
    /// <summary>The sender of the access codes in these tests: it keeps the code in memory so the test can type it.</summary>
    public FakeAccessCodeSender AccessCodes { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Registered by the same method the Api uses, so the tests also prove the registration.
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IIdentityProvider>(Identity)
            .AddScoped<FakeCurrentUser>()
            .AddScoped<ICurrentUser>(provider => provider.GetRequiredService<FakeCurrentUser>())
            .AddSingleton<IAccessCodeSender>(AccessCodes)
            .AddCustomerTokenIssuer(TestTokens.Options)
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

    // MySQL logs every InnoDB deadlock (innodb-print-all-deadlocks, notes need verbosity 3). When there is one,
    // keep it in a file next to the test binaries, so the two statements that collided can be read after the run.
    // The tests that race two bookings of one professional on purpose may leave one: that is the expected outcome.
    private async Task ReportDeadlocksAsync()
    {
        try
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            var lines = $"{stdout}\n{stderr}".Split('\n');
            var report = new StringBuilder();

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("deadlock detected", StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine(string.Join('\n', lines.Skip(i).Take(80)));
                    report.AppendLine(new string('-', 40));
                }
            }

            if (report.Length > 0)
            {
                var path = Path.Combine(AppContext.BaseDirectory, $"mysql-deadlocks-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                await File.WriteAllTextAsync(path, report.ToString());
                Console.WriteLine($"MySQL deadlock report written to {path}");
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