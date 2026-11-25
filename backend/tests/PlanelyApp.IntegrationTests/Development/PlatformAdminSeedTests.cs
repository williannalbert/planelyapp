using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Development;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Development;

/// <summary>
/// The platform administrator of the development environment: the user of the realm that Keycloak imports has to exist in the
/// database too, or the first call to the Api is a 403. Creating it twice must change nothing.
/// </summary>
public class PlatformAdminSeedTests(MySqlFixture mysql)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static DevelopmentSeedOptions NewOptions() => new()
    {
        IdentityId = $"sub-{Guid.NewGuid():N}",
        Name = "Development Admin",
        Email = StaffData.NewEmail(),
    };

    private async Task<bool> EnsureAsync(DevelopmentSeedOptions options)
    {
        await using var scope = mysql.CreateScope();
        var seed = new PlatformAdminSeed(
            scope.ServiceProvider.GetRequiredService<IUserRepository>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

        return await seed.EnsureAsync(options, _ct);
    }

    private async Task<User?> FindAsync(string identityId)
    {
        await using var scope = mysql.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdentityIdAsync(identityId, _ct);
    }

    [Fact]
    public async Task It_should_create_the_platform_administrator_when_there_is_none()
    {
        var options = NewOptions();

        var created = await EnsureAsync(options);
        var user = await FindAsync(options.IdentityId);

        created.ShouldBeTrue();
        user.ShouldNotBeNull();
        user.Role.ShouldBe(UserRole.PlatformAdmin);
        user.CompanyId.ShouldBeNull();
        user.Name.ShouldBe("Development Admin");
        user.Email.Value.ShouldBe(options.Email);
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_second_run_should_create_nothing_and_keep_the_user_as_it_is()
    {
        var options = NewOptions();
        await EnsureAsync(options);
        var first = await FindAsync(options.IdentityId);

        var createdAgain = await EnsureAsync(options);
        var second = await FindAsync(options.IdentityId);

        createdAgain.ShouldBeFalse();
        second!.Id.ShouldBe(first!.Id);
    }

    [Fact]
    public async Task The_background_service_should_not_stop_the_application_when_the_seed_fails()
    {
        var services = new ServiceCollection();
        services.AddScoped<PlatformAdminSeed>(_ => throw new InvalidOperationException("the database is down"));
        await using var provider = services.BuildServiceProvider();
        using var service = new DevelopmentSeedService(
            provider.GetRequiredService<IServiceScopeFactory>(), NewOptions(), NullLogger<DevelopmentSeedService>.Instance);

        await service.StartAsync(_ct);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), _ct);
        await service.StopAsync(_ct);

        service.ExecuteTask!.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "Admin", "admin@planelyapp.local")]
    [InlineData("sub-1", " ", "admin@planelyapp.local")]
    [InlineData("sub-1", "Admin", "")]
    public void The_options_should_be_complete_before_the_application_starts(string identityId, string name, string email)
    {
        var options = new DevelopmentSeedOptions { IdentityId = identityId, Name = name, Email = email };

        Should.Throw<ArgumentException>(() => new ServiceCollection().AddDevelopmentSeed(options));
    }
}
