using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Identity;

public class AccessCodeTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static IssuedAccessCode Issue(IEnumerable<DateTime>? recent = null, DateTime? now = null) =>
        AccessCode.Issue(
            CompanyId, AccessChannel.Email, "ana@example.com", recent ?? Array.Empty<DateTime>(), now ?? Now);

    private static string WrongCode(string plainCode) => plainCode == "000000" ? "000001" : "000000";

    // ---- Issuing ----

    [Fact]
    public void Issue_should_create_a_code_of_six_digits_that_expires_in_five_minutes()
    {
        var issued = Issue();

        issued.PlainCode.Length.ShouldBe(6);
        issued.PlainCode.ShouldAllBe(character => char.IsAsciiDigit(character));
        issued.AccessCode.CompanyId.ShouldBe(CompanyId);
        issued.AccessCode.Channel.ShouldBe(AccessChannel.Email);
        issued.AccessCode.CreatedAtUtc.ShouldBe(Now);
        issued.AccessCode.ExpiresAtUtc.ShouldBe(Now.AddMinutes(5));
        issued.AccessCode.Attempts.ShouldBe(0);
        issued.AccessCode.UsedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Issue_should_save_only_a_hash_of_the_code()
    {
        var issued = Issue();

        issued.AccessCode.CodeHash.Length.ShouldBe(64);
        issued.AccessCode.CodeHash.ShouldNotBe(issued.PlainCode);
    }

    [Fact]
    public void Issue_should_normalize_the_destination()
    {
        AccessCode.Issue(CompanyId, AccessChannel.Email, "  Ana@Example.com ", [], Now)
            .AccessCode.Destination.ShouldBe("ana@example.com");

        AccessCode.Issue(CompanyId, AccessChannel.Phone, "+55 (11) 99999-0000", [], Now)
            .AccessCode.Destination.ShouldBe("+5511999990000");
    }

    [Fact]
    public void Issue_should_reject_a_destination_that_does_not_match_the_channel()
    {
        Should.Throw<DomainException>(() => AccessCode.Issue(CompanyId, AccessChannel.Email, "not-an-email", [], Now))
            .Code.ShouldBe("Email.Invalid");

        Should.Throw<DomainException>(() => AccessCode.Issue(CompanyId, AccessChannel.Phone, "ana@example.com", [], Now))
            .Code.ShouldBe("Phone.Invalid");
    }

    [Fact]
    public void Issue_should_reject_invalid_arguments()
    {
        Should.Throw<DomainException>(() => AccessCode.Issue(Guid.Empty, AccessChannel.Email, "ana@example.com", [], Now))
            .Code.ShouldBe("AccessCode.CompanyRequired");

        Should.Throw<DomainException>(() => AccessCode.Issue(CompanyId, (AccessChannel)99, "ana@example.com", [], Now))
            .Code.ShouldBe("AccessCode.ChannelInvalid");

        Should.Throw<DomainException>(() => AccessCode.Issue(
                CompanyId, AccessChannel.Email, "ana@example.com", [], new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified)))
            .Code.ShouldBe("AccessCode.TimeMustBeUtc");

        Should.Throw<DomainException>(() => Issue(recent: [new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Unspecified)]))
            .Code.ShouldBe("AccessCode.TimeMustBeUtc");
    }

    // ---- Resending (RN22) ----

    [Fact]
    public void A_second_code_within_a_minute_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Issue(recent: [Now.AddSeconds(-30)]))
            .Code.ShouldBe("AccessCode.ResendTooSoon");
    }

    [Fact]
    public void A_second_code_exactly_one_minute_later_should_be_allowed()
    {
        Issue(recent: [Now.AddMinutes(-1)]).AccessCode.ShouldNotBeNull();
    }

    [Fact]
    public void The_sixth_code_within_an_hour_should_be_rejected()
    {
        var recent = new[]
        {
            Now.AddMinutes(-5), Now.AddMinutes(-15), Now.AddMinutes(-25), Now.AddMinutes(-35), Now.AddMinutes(-45)
        };

        Should.Throw<DomainException>(() => Issue(recent: recent))
            .Code.ShouldBe("AccessCode.ResendLimitReached");
    }

    [Fact]
    public void The_fifth_code_within_an_hour_should_be_allowed()
    {
        var recent = new[] { Now.AddMinutes(-5), Now.AddMinutes(-15), Now.AddMinutes(-25), Now.AddMinutes(-35) };

        Issue(recent: recent).AccessCode.ShouldNotBeNull();
    }

    [Fact]
    public void A_code_issued_exactly_one_hour_ago_should_no_longer_count()
    {
        var recent = new[]
        {
            Now.AddMinutes(-60), Now.AddMinutes(-45), Now.AddMinutes(-30), Now.AddMinutes(-15), Now.AddMinutes(-5)
        };

        Issue(recent: recent).AccessCode.ShouldNotBeNull();
    }

    // ---- Verifying (RN21, RN22) ----

    [Fact]
    public void The_right_code_should_be_verified_and_marked_as_used()
    {
        var issued = Issue();

        var result = issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(1));

        result.ShouldBe(AccessCodeVerification.Verified);
        issued.AccessCode.UsedAtUtc.ShouldBe(Now.AddMinutes(1));
        issued.AccessCode.Attempts.ShouldBe(0);
    }

    [Fact]
    public void A_code_should_work_only_once()
    {
        var issued = Issue();
        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(1));

        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(2)).ShouldBe(AccessCodeVerification.AlreadyUsed);
    }

    [Fact]
    public void A_wrong_code_should_count_as_an_attempt_and_leave_the_code_usable()
    {
        var issued = Issue();

        issued.AccessCode.Verify(WrongCode(issued.PlainCode), Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Invalid);

        issued.AccessCode.Attempts.ShouldBe(1);
        issued.AccessCode.UsedAtUtc.ShouldBeNull();
        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public void The_right_code_on_the_fifth_attempt_should_still_work()
    {
        var issued = Issue();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            issued.AccessCode.Verify(WrongCode(issued.PlainCode), Now.AddMinutes(1));
        }

        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public void After_five_wrong_attempts_even_the_right_code_should_be_refused()
    {
        var issued = Issue();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            issued.AccessCode.Verify(WrongCode(issued.PlainCode), Now.AddMinutes(1))
                .ShouldBe(AccessCodeVerification.Invalid);
        }

        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.TooManyAttempts);
        issued.AccessCode.Attempts.ShouldBe(5);
        issued.AccessCode.UsedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void An_expired_code_should_be_refused_without_counting_an_attempt()
    {
        var issued = Issue();

        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(5)).ShouldBe(AccessCodeVerification.Expired);
        issued.AccessCode.Verify(WrongCode(issued.PlainCode), Now.AddMinutes(6)).ShouldBe(AccessCodeVerification.Expired);
        issued.AccessCode.Attempts.ShouldBe(0);
    }

    [Fact]
    public void The_code_should_still_work_up_to_the_last_instant_before_it_expires()
    {
        var issued = Issue();

        issued.AccessCode.IsExpired(Now.AddMinutes(5).AddTicks(-1)).ShouldBeFalse();
        issued.AccessCode.IsExpired(Now.AddMinutes(5)).ShouldBeTrue();
        issued.AccessCode.Verify(issued.PlainCode, Now.AddMinutes(5).AddTicks(-1))
            .ShouldBe(AccessCodeVerification.Verified);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("１２３４５６")]
    public void A_malformed_code_should_be_invalid_and_count_as_an_attempt(string? malformed)
    {
        var issued = Issue();

        issued.AccessCode.Verify(malformed, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Invalid);

        issued.AccessCode.Attempts.ShouldBe(1);
        issued.AccessCode.UsedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Spaces_around_the_right_code_should_be_ignored()
    {
        var issued = Issue();

        issued.AccessCode.Verify($"  {issued.PlainCode} ", Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public void Verify_should_reject_an_instant_that_is_not_utc()
    {
        var issued = Issue();

        Should.Throw<DomainException>(() => issued.AccessCode.Verify(
                issued.PlainCode, new DateTime(2026, 11, 2, 12, 1, 0, DateTimeKind.Unspecified)))
            .Code.ShouldBe("AccessCode.TimeMustBeUtc");
    }

    [Fact]
    public void The_text_of_the_issued_code_should_hide_the_secret()
    {
        var issued = Issue();

        issued.ToString().ShouldNotContain(issued.PlainCode);
    }
}