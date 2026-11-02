using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class KeycloakIdentityProviderTests(KeycloakFixture keycloak) : IClassFixture<KeycloakFixture>
{
    private static string NewEmail() => $"person{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task An_account_should_be_created_in_the_realm_and_receive_the_first_access_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();

        var created = await keycloak.Provider.CreateUserAsync(
            new IdentityProfile("Bia Lopes", Email.Create(email), Language.Spanish), cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var account = await keycloak.GetAccountAsync(created.Value, cancellationToken);
        account.ShouldNotBeNull();
        account["email"]!.GetValue<string>().ShouldBe(email);
        account["username"]!.GetValue<string>().ShouldBe(email);
        account["firstName"]!.GetValue<string>().ShouldBe("Bia Lopes");
        account["enabled"]!.GetValue<bool>().ShouldBeTrue();
        account["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("es");
        (await keycloak.WaitForEmailSubjectAsync(email, cancellationToken)).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_same_email_should_not_get_a_second_account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = new IdentityProfile("Bia Lopes", Email.Create(NewEmail()), null);
        await keycloak.Provider.CreateUserAsync(profile, cancellationToken);

        var again = await keycloak.Provider.CreateUserAsync(profile, cancellationToken);

        again.IsFailure.ShouldBeTrue();
        again.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
    }

    [Fact]
    public async Task An_update_should_change_the_profile_and_the_login_and_free_the_old_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var oldEmail = NewEmail();
        var newEmail = NewEmail();
        var created = await keycloak.Provider.CreateUserAsync(
            new IdentityProfile("Bia Lopes", Email.Create(oldEmail), Language.English), cancellationToken);

        var updated = await keycloak.Provider.UpdateUserAsync(
            created.Value,
            new IdentityProfile("Beatriz Lopes", Email.Create(newEmail), Language.PortugueseBrazil),
            cancellationToken);

        updated.IsSuccess.ShouldBeTrue();
        var account = await keycloak.GetAccountAsync(created.Value, cancellationToken);
        account!["email"]!.GetValue<string>().ShouldBe(newEmail);
        account["username"]!.GetValue<string>().ShouldBe(newEmail);
        account["firstName"]!.GetValue<string>().ShouldBe("Beatriz Lopes");
        account["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("pt-BR");

        var reused = await keycloak.Provider.CreateUserAsync(
            new IdentityProfile("Other", Email.Create(oldEmail), null), cancellationToken);
        reused.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Disabling_and_enabling_should_change_only_the_enabled_flag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await keycloak.Provider.CreateUserAsync(
            new IdentityProfile("Bia Lopes", Email.Create(NewEmail()), Language.Spanish), cancellationToken);

        await keycloak.Provider.SetEnabledAsync(created.Value, false, cancellationToken);
        var disabled = await keycloak.GetAccountAsync(created.Value, cancellationToken);

        await keycloak.Provider.SetEnabledAsync(created.Value, true, cancellationToken);
        var enabled = await keycloak.GetAccountAsync(created.Value, cancellationToken);

        disabled!["enabled"]!.GetValue<bool>().ShouldBeFalse();
        disabled["firstName"]!.GetValue<string>().ShouldBe("Bia Lopes");
        disabled["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("es");
        enabled!["enabled"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task A_deleted_account_should_be_gone_and_deleting_it_again_should_not_fail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await keycloak.Provider.CreateUserAsync(
            new IdentityProfile("Bia Lopes", Email.Create(NewEmail()), null), cancellationToken);

        await keycloak.Provider.DeleteUserAsync(created.Value, cancellationToken);

        (await keycloak.GetAccountAsync(created.Value, cancellationToken)).ShouldBeNull();
        await Should.NotThrowAsync(() => keycloak.Provider.DeleteUserAsync(created.Value, cancellationToken));
    }
}