using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Identity;

public class CustomerSessionTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static OpenedSession Open() => CustomerSession.Open(CompanyId, CustomerId, Lifetime, Now);

    [Fact]
    public void Open_should_start_an_active_session_with_a_fixed_expiry()
    {
        var opened = Open();

        opened.Session.CompanyId.ShouldBe(CompanyId);
        opened.Session.CustomerId.ShouldBe(CustomerId);
        opened.Session.CreatedAtUtc.ShouldBe(Now);
        opened.Session.ExpiresAtUtc.ShouldBe(Now.Add(Lifetime));
        opened.Session.RevokedAtUtc.ShouldBeNull();
        opened.Session.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void Open_should_save_only_the_hash_of_the_refresh_token()
    {
        var opened = Open();

        opened.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        opened.Session.RefreshTokenHash.ShouldNotBe(opened.RefreshToken);
        opened.Session.RefreshTokenHash.ShouldBe(CustomerSession.HashToken(opened.RefreshToken));
    }

    [Fact]
    public void Each_session_should_get_a_different_token()
    {
        Open().RefreshToken.ShouldNotBe(Open().RefreshToken);
    }

    [Fact]
    public void Open_should_reject_invalid_arguments()
    {
        Should.Throw<DomainException>(() => CustomerSession.Open(Guid.Empty, CustomerId, Lifetime, Now))
            .Code.ShouldBe("CustomerSession.CompanyRequired");
        Should.Throw<DomainException>(() => CustomerSession.Open(CompanyId, Guid.Empty, Lifetime, Now))
            .Code.ShouldBe("CustomerSession.CustomerRequired");
        Should.Throw<DomainException>(() => CustomerSession.Open(CompanyId, CustomerId, TimeSpan.Zero, Now))
            .Code.ShouldBe("CustomerSession.LifetimeInvalid");
        Should.Throw<DomainException>(() => CustomerSession.Open(CompanyId, CustomerId, TimeSpan.FromDays(400), Now))
            .Code.ShouldBe("CustomerSession.LifetimeInvalid");
        Should.Throw<DomainException>(() => CustomerSession.Open(
                CompanyId, CustomerId, Lifetime, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified)))
            .Code.ShouldBe("CustomerSession.TimeMustBeUtc");
    }

    [Fact]
    public void HashToken_should_be_stable_and_reject_an_empty_token()
    {
        CustomerSession.HashToken("abc").ShouldBe(CustomerSession.HashToken("abc"));
        CustomerSession.HashToken("abc").ShouldNotBe(CustomerSession.HashToken("abd"));

        Should.Throw<DomainException>(() => CustomerSession.HashToken(" "))
            .Code.ShouldBe("CustomerSession.RefreshTokenRequired");
        Should.Throw<DomainException>(() => CustomerSession.HashToken(null))
            .Code.ShouldBe("CustomerSession.RefreshTokenRequired");
    }

    [Fact]
    public void Renew_should_replace_the_token_and_keep_the_expiry()
    {
        var opened = Open();
        var oldHash = opened.Session.RefreshTokenHash;

        var newToken = opened.Session.Renew(Now.AddDays(1));

        newToken.ShouldNotBe(opened.RefreshToken);
        opened.Session.RefreshTokenHash.ShouldBe(CustomerSession.HashToken(newToken));
        opened.Session.RefreshTokenHash.ShouldNotBe(oldHash);
        opened.Session.ExpiresAtUtc.ShouldBe(Now.Add(Lifetime));
    }

    [Fact]
    public void Renew_should_be_refused_after_the_session_is_revoked()
    {
        var opened = Open();
        opened.Session.Revoke(Now.AddDays(1));
        var hash = opened.Session.RefreshTokenHash;

        Should.Throw<DomainException>(() => opened.Session.Renew(Now.AddDays(2)))
            .Code.ShouldBe("CustomerSession.Revoked");

        opened.Session.RefreshTokenHash.ShouldBe(hash);
    }

    [Fact]
    public void Renew_should_be_refused_once_the_session_has_expired()
    {
        var opened = Open();

        opened.Session.Renew(Now.Add(Lifetime).AddTicks(-1)).ShouldNotBeNullOrWhiteSpace();

        Should.Throw<DomainException>(() => opened.Session.Renew(Now.Add(Lifetime)))
            .Code.ShouldBe("CustomerSession.Expired");
    }

    [Fact]
    public void Revoke_should_end_the_session_and_keep_the_first_instant()
    {
        var opened = Open();

        opened.Session.Revoke(Now.AddDays(1));
        opened.Session.Revoke(Now.AddDays(2));

        opened.Session.RevokedAtUtc.ShouldBe(Now.AddDays(1));
        opened.Session.IsActive(Now.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_session_should_stop_being_active_at_its_expiry()
    {
        var opened = Open();

        opened.Session.IsActive(Now.Add(Lifetime).AddTicks(-1)).ShouldBeTrue();
        opened.Session.IsActive(Now.Add(Lifetime)).ShouldBeFalse();
    }

    [Fact]
    public void The_text_of_the_opened_session_should_hide_the_token()
    {
        var opened = Open();

        opened.ToString().ShouldNotContain(opened.RefreshToken);
    }
}