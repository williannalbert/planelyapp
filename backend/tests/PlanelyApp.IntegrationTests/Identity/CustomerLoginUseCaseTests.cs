using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Identity.AccessCodes.Request;
using PlanelyApp.Application.Identity.AccessCodes.Verify;
using PlanelyApp.Application.Identity.Sessions.Logout;
using PlanelyApp.Application.Identity.Sessions.Refresh;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Messaging;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

/// <summary>The login of the customer end to end: the codes, the customer of the first access, the tokens and the limits.</summary>
public class CustomerLoginUseCaseTests(MySqlFixture mysql)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_access_should_register_the_customer_with_only_the_contact_and_open_a_session()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        await RequestAsync(company, email);

        var result = await VerifyAsync(company, email, mysql.AccessCodes.CodeFor(company, email));

        result.Value.Outcome.ShouldBe(AccessCodeVerification.Verified);
        var login = result.Value.Login.ShouldNotBeNull();
        login.IsNewCustomer.ShouldBeTrue();

        await using var scope = mysql.CreateScope(company);
        var customer = (await scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .GetByIdAsync(login.CustomerId, _ct))!;
        customer.Name.ShouldBeNull();
        customer.Phone.ShouldBeNull();
        customer.Email.ShouldNotBeNull().Value.ShouldBe(email);
        customer.LastAccessAt.ShouldNotBeNull();

        var session = await scope.ServiceProvider.GetRequiredService<ICustomerSessionRepository>()
            .GetByRefreshTokenHashAsync(company, CustomerSession.HashToken(login.RefreshToken), _ct);
        session.ShouldNotBeNull().CustomerId.ShouldBe(login.CustomerId);

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(login.AccessToken, TestTokens.Validation(TestTokens.Options));
        validation.IsValid.ShouldBeTrue();
        validation.Claims[JwtRegisteredClaimNames.Sub].ShouldBe(login.CustomerId.ToString());
        validation.Claims[CustomerTokenClaims.CompanyId].ShouldBe(company.ToString());

        (await OutboxData.ForCompanyAsync(mysql, company, _ct)).Select(message => message.Type)
            .ShouldContain("customer.registered");
    }

    [Fact]
    public async Task A_customer_who_already_exists_should_be_logged_in_instead_of_registered_again()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        var existing = (await mysql.SendAsync(company, new RegisterCustomerCommand(Email: email), _ct)).Value;

        var login = await LoginAsync(company, email);

        login.IsNewCustomer.ShouldBeFalse();
        login.CustomerId.ShouldBe(existing);
    }

    [Fact]
    public async Task Wrong_attempts_should_be_saved_and_lock_the_code_even_for_the_right_one()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        await RequestAsync(company, email);
        var code = mysql.AccessCodes.CodeFor(company, email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < AccessCode.MaxAttempts; attempt++)
        {
            (await VerifyAsync(company, email, wrong)).Value.Outcome.ShouldBe(AccessCodeVerification.Invalid);
        }

        var locked = (await VerifyAsync(company, email, code)).Value;

        locked.Outcome.ShouldBe(AccessCodeVerification.TooManyAttempts);
        locked.Login.ShouldBeNull();
        (await LatestCodeAsync(company, email))!.Attempts.ShouldBe(AccessCode.MaxAttempts);
    }

    [Fact]
    public async Task A_code_should_work_only_once()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        await LoginAsync(company, email);

        var second = await VerifyAsync(company, email, mysql.AccessCodes.CodeFor(company, email));

        second.Value.Outcome.ShouldBe(AccessCodeVerification.AlreadyUsed);
    }

    [Fact]
    public async Task A_code_of_one_company_should_not_work_in_another()
    {
        var company = await NewCompanyAsync();
        var other = await NewCompanyAsync();
        var email = NewEmail();
        await RequestAsync(company, email);
        var code = mysql.AccessCodes.CodeFor(company, email);

        var inTheOther = await VerifyAsync(other, email, code);

        inTheOther.Value.Outcome.ShouldBe(AccessCodeVerification.Invalid);
        (await VerifyAsync(company, email, code)).Value.Outcome.ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public async Task A_second_code_within_a_minute_should_be_refused_and_not_sent()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        await RequestAsync(company, email);

        var exception = await Should.ThrowAsync<DomainException>(() => RequestAsync(company, email));

        exception.Code.ShouldBe("AccessCode.ResendTooSoon");
        mysql.AccessCodes.SentTo(company, email).ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_delivery_should_save_nothing_and_leave_the_customer_free_to_ask_again()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        mysql.AccessCodes.FailFor(email);

        await Should.ThrowAsync<AccessCodeDeliveryException>(() => RequestAsync(company, email));

        (await LatestCodeAsync(company, email)).ShouldBeNull();

        mysql.AccessCodes.Recover(email);
        await RequestAsync(company, email); // no ResendTooSoon: the failed one was never saved
        mysql.AccessCodes.SentTo(company, email).ShouldBe(1);
    }

    [Fact]
    public async Task The_refresh_token_should_rotate_and_only_the_newest_one_should_work()
    {
        var company = await NewCompanyAsync();
        var login = await LoginAsync(company, NewEmail());

        var first = (await RefreshAsync(company, login.RefreshToken)).Value;
        first.RefreshToken.ShouldNotBe(login.RefreshToken);
        first.SessionExpiresAtUtc.ShouldBe(login.SessionExpiresAtUtc, TimeSpan.FromSeconds(1));

        var old = await RefreshAsync(company, login.RefreshToken);
        old.Error.Type.ShouldBe(ErrorType.Unauthorized);
        old.Error.Code.ShouldBe("CustomerSession.RefreshTokenInvalid");

        (await RefreshAsync(company, first.RefreshToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Logging_out_should_end_the_session_and_be_safe_to_repeat()
    {
        var company = await NewCompanyAsync();
        var login = await LoginAsync(company, NewEmail());

        (await mysql.SendAsync(company, new LogoutCustomerCommand(login.RefreshToken), _ct)).IsSuccess.ShouldBeTrue();

        (await RefreshAsync(company, login.RefreshToken)).Error.Type.ShouldBe(ErrorType.Unauthorized);
        (await mysql.SendAsync(company, new LogoutCustomerCommand(login.RefreshToken), _ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_same_code_verified_twice_at_the_same_time_should_log_in_only_once()
    {
        var company = await NewCompanyAsync();
        var email = NewEmail();
        await RequestAsync(company, email);
        var code = mysql.AccessCodes.CodeFor(company, email);

        var results = await Task.WhenAll(VerifiedAsync(company, email, code), VerifiedAsync(company, email, code));

        results.Count(verified => verified).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_refresh_token_used_twice_at_the_same_time_should_work_only_once()
    {
        var company = await NewCompanyAsync();
        var login = await LoginAsync(company, NewEmail());

        var results = await Task.WhenAll(
            RefreshedAsync(company, login.RefreshToken), RefreshedAsync(company, login.RefreshToken));

        results.Count(refreshed => refreshed).ShouldBe(1);
    }

    private static string NewEmail() => $"login-{Guid.NewGuid():N}@example.com";

    private async Task<Guid> NewCompanyAsync() => (await OrganizationData.SeedCompanyAsync(mysql, _ct)).Id;

    private async Task RequestAsync(Guid company, string email) =>
        (await mysql.SendAsync(company, new RequestAccessCodeCommand(AccessChannel.Email, email), _ct))
            .IsSuccess.ShouldBeTrue();

    private Task<Result<VerifyAccessCodeResponse>> VerifyAsync(Guid company, string email, string code) =>
        mysql.SendAsync(company, new VerifyAccessCodeCommand(AccessChannel.Email, email, code), _ct);

    private Task<Result<CustomerTokens>> RefreshAsync(Guid company, string refreshToken) =>
        mysql.SendAsync(company, new RefreshCustomerSessionCommand(refreshToken), _ct);

    private async Task<CustomerLogin> LoginAsync(Guid company, string email)
    {
        await RequestAsync(company, email);
        var result = await VerifyAsync(company, email, mysql.AccessCodes.CodeFor(company, email));
        result.Value.Outcome.ShouldBe(AccessCodeVerification.Verified);

        return result.Value.Login!;
    }

    private async Task<AccessCode?> LatestCodeAsync(Guid company, string email)
    {
        await using var scope = mysql.CreateScope(company);

        return await scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetLatestAsync(company, email, _ct);
    }

    // The one that loses the race has its save refused (the concurrency token) and reports false.
    private async Task<bool> VerifiedAsync(Guid company, string email, string code)
    {
        try
        {
            return (await VerifyAsync(company, email, code)).Value.Outcome == AccessCodeVerification.Verified;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private async Task<bool> RefreshedAsync(Guid company, string refreshToken)
    {
        try
        {
            return (await RefreshAsync(company, refreshToken)).IsSuccess;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}