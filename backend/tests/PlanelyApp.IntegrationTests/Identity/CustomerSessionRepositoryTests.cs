using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class CustomerSessionRepositoryTests(MySqlFixture mysql)
{
    private static readonly DateTime Now = DateTime.UtcNow.Date.AddHours(8);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    [Fact]
    public async Task A_saved_session_should_come_back_and_be_found_by_its_refresh_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, customer) = await SeedAsync(cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var opened = CustomerSession.Open(company.Id, customer.Id, Lifetime, Now);
        await SaveAsync(company.Id, opened.Session, cancellationToken);

        var loaded = await LoadAsync(company.Id, opened.Session.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.CustomerId.ShouldBe(customer.Id);
        loaded.RefreshTokenHash.ShouldBe(opened.Session.RefreshTokenHash);
        loaded.CreatedAtUtc.ShouldBe(Now);
        loaded.ExpiresAtUtc.ShouldBe(Now + Lifetime);
        loaded.ExpiresAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.RevokedAtUtc.ShouldBeNull();
        loaded.IsActive(Now.AddDays(1)).ShouldBeTrue();
        loaded.IsActive(Now.AddDays(31)).ShouldBeFalse();

        var hash = CustomerSession.HashToken(opened.RefreshToken);

        await using var scope = mysql.CreateScope(company.Id);
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>();

        (await repository.GetByRefreshTokenHashAsync(company.Id, hash, cancellationToken))!.Id.ShouldBe(opened.Session.Id);
        (await repository.GetByRefreshTokenHashAsync(company.Id, CustomerSession.HashToken("unknown-token"), cancellationToken)).ShouldBeNull();
        (await repository.GetByRefreshTokenHashAsync(other.Id, hash, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Renewing_the_token_should_stop_the_old_one_from_working()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, customer) = await SeedAsync(cancellationToken);
        var opened = CustomerSession.Open(company.Id, customer.Id, Lifetime, Now);
        await SaveAsync(company.Id, opened.Session, cancellationToken);

        var newToken = string.Empty;
        await UpdateAsync(company.Id, opened.Session.Id, cancellationToken, loaded => newToken = loaded.Renew(Now.AddDays(1)));

        await using var scope = mysql.CreateScope(company.Id);
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>();

        (await repository.GetByRefreshTokenHashAsync(company.Id, CustomerSession.HashToken(opened.RefreshToken), cancellationToken)).ShouldBeNull();
        (await repository.GetByRefreshTokenHashAsync(company.Id, CustomerSession.HashToken(newToken), cancellationToken))!
            .Id.ShouldBe(opened.Session.Id);
    }

    [Fact]
    public async Task Revoking_the_session_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, customer) = await SeedAsync(cancellationToken);
        var opened = CustomerSession.Open(company.Id, customer.Id, Lifetime, Now);
        await SaveAsync(company.Id, opened.Session, cancellationToken);

        await UpdateAsync(company.Id, opened.Session.Id, cancellationToken, loaded => loaded.Revoke(Now.AddHours(2)));

        var revoked = await LoadAsync(company.Id, opened.Session.Id, cancellationToken);

        revoked.ShouldNotBeNull();
        revoked.RevokedAtUtc.ShouldBe(Now.AddHours(2));
        revoked.IsActive(Now.AddHours(3)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_session_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, customer) = await SeedAsync(cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var opened = CustomerSession.Open(company.Id, customer.Id, Lifetime, Now);
        await SaveAsync(company.Id, opened.Session, cancellationToken);

        (await LoadAsync(other.Id, opened.Session.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, opened.Session.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_of_a_customer_that_does_not_exist_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, _) = await SeedAsync(cancellationToken);
        var opened = CustomerSession.Open(company.Id, Guid.CreateVersion7(), Lifetime, Now);

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(company.Id, opened.Session, cancellationToken));
    }

    [Fact]
    public async Task Two_simultaneous_renewals_of_the_same_token_should_not_both_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, customer) = await SeedAsync(cancellationToken);
        var opened = CustomerSession.Open(company.Id, customer.Id, Lifetime, Now);
        await SaveAsync(company.Id, opened.Session, cancellationToken);

        await using var first = mysql.CreateScope(company.Id);
        await using var second = mysql.CreateScope(company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<ICustomerSessionRepository>()
            .GetByIdAsync(opened.Session.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<ICustomerSessionRepository>()
            .GetByIdAsync(opened.Session.Id, cancellationToken);

        var firstToken = seenByFirst!.Renew(Now.AddDays(1));
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        seenBySecond!.Renew(Now.AddDays(1));

        // The old token was already replaced: a second valid token must not come out of it.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        await using var scope = mysql.CreateScope(company.Id);
        (await scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>()
            .GetByRefreshTokenHashAsync(company.Id, CustomerSession.HashToken(firstToken), cancellationToken)).ShouldNotBeNull();
    }

    private async Task<(Company Company, Customer Customer)> SeedAsync(CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var customer = StaffData.NewCustomer(company.Id);

        await StaffData.SaveAsync(mysql, company.Id, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(customer));

        return (company, customer);
    }

    private Task SaveAsync(Guid companyId, CustomerSession session, CancellationToken cancellationToken) =>
        StaffData.SaveAsync(mysql, companyId, cancellationToken,
            services => services.GetRequiredService<ICustomerSessionRepository>().Add(session));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<CustomerSession> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<CustomerSession?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>().GetByIdAsync(id, cancellationToken);
    }
}