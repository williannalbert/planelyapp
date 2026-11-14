using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

public class UnitOfWorkTests(MySqlFixture mysql)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Discarding_the_changes_should_forget_what_was_not_saved()
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, _ct);
        await using var scope = mysql.CreateScope(company.Id);
        var db = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();
        var loaded = await db.Set<Company>().SingleAsync(entity => entity.Id == company.Id, _ct);
        loaded.Deactivate();
        db.ChangeTracker.HasChanges().ShouldBeTrue();

        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().DiscardChanges();

        db.ChangeTracker.Entries().ShouldBeEmpty();
        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(_ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_save_that_waits_too_long_for_a_lock_should_be_a_concurrency_conflict()
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, _ct);

        // Someone else holds the row of the company and does not let go...
        await using var holder = new MySqlConnection(mysql.ConnectionString);
        await holder.OpenAsync(_ct);
        await using var transaction = await holder.BeginTransactionAsync(_ct);

        await using (var command = new MySqlCommand("SELECT id FROM company WHERE id = @id FOR UPDATE", holder, transaction))
        {
            command.Parameters.AddWithValue("@id", company.Id.ToString());
            await command.ExecuteScalarAsync(_ct);
        }

        // ...and this request waits for it one second instead of the fifty the server allows.
        await using var scope = mysql.CreateScope(company.Id);
        var db = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();
        await db.Database.OpenConnectionAsync(_ct);
        await db.Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = 1", _ct);
        var loaded = await db.Set<Company>().SingleAsync(entity => entity.Id == company.Id, _ct);
        loaded.Deactivate();

        var failure = await Should.ThrowAsync<ConcurrencyConflictException>(() => db.SaveChangesAsync(_ct));

        failure.InnerException.ShouldNotBeNull();
    }
}
