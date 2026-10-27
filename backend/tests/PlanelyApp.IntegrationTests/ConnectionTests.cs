using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests;

public class ConnectionTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_context_should_connect_to_the_database()
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var canConnect = await context.Database.CanConnectAsync(TestContext.Current.CancellationToken);

        canConnect.ShouldBeTrue();
    }

    [Fact]
    public async Task The_server_should_be_the_version_the_context_is_configured_for()
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var version = await context.Database
            .SqlQueryRaw<string>("SELECT VERSION() AS Value")
            .SingleAsync(TestContext.Current.CancellationToken);

        version.ShouldStartWith("8.4");
    }

    [Fact]
    public async Task The_database_should_store_text_as_utf8mb4()
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var charset = await context.Database
            .SqlQueryRaw<string>("SELECT @@character_set_database AS Value")
            .SingleAsync(TestContext.Current.CancellationToken);

        // Accents, emoji and any language of the three we support.
        charset.ShouldBe("utf8mb4");
    }

    [Fact]
    public async Task The_unit_of_work_should_be_the_context_the_repositories_use()
    {
        await using var scope = mysql.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        unitOfWork.ShouldBeSameAs(context);
    }

    [Fact]
    public async Task Each_scope_should_get_its_own_context()
    {
        await using var first = mysql.CreateScope();
        await using var second = mysql.CreateScope();

        var contextOfFirst = first.ServiceProvider.GetRequiredService<PlanelyDbContext>();
        var contextOfSecond = second.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        contextOfFirst.ShouldNotBeSameAs(contextOfSecond);
        await Task.CompletedTask;
    }
}