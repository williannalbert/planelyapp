using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Identity.Sessions.Refresh;
using PlanelyApp.Application.Tests.Organization.Branches;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Identity.Sessions;
using Shouldly;

namespace PlanelyApp.Application.Tests.Identity;

public class RefreshCustomerSessionTests
{
    private static readonly DateTime Opened = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Opened.AddDays(1);
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private readonly ICustomerSessionRepository _sessions = Substitute.For<ICustomerSessionRepository>();
    private readonly ICustomerTokenIssuer _tokens = Substitute.For<ICustomerTokenIssuer>();

    public RefreshCustomerSessionTests() =>
        _tokens.Issue(CompanyId, CustomerId, Now).Returns(new CustomerAccessToken("access-token", Now.AddMinutes(15)));

    [Fact]
    public async Task A_valid_refresh_token_should_be_traded_for_new_tokens_and_the_old_one_stops_working()
    {
        var opened = Open();

        var result = await RefreshAsync(opened.RefreshToken);

        result.IsSuccess.ShouldBeTrue();
        var tokens = result.Value;
        tokens.AccessToken.ShouldBe("access-token");
        tokens.RefreshToken.ShouldNotBe(opened.RefreshToken);
        tokens.SessionExpiresAtUtc.ShouldBe(Opened.AddDays(30)); // renewing never extends the session
        opened.Session.RefreshTokenHash.ShouldBe(CustomerSession.HashToken(tokens.RefreshToken));
        opened.Session.RefreshTokenHash.ShouldNotBe(CustomerSession.HashToken(opened.RefreshToken));
        await _sessions.Received(1).GetByRefreshTokenHashAsync(
            CompanyId, CustomerSession.HashToken(opened.RefreshToken), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_token_should_be_unauthorized()
    {
        Open();

        var result = await RefreshAsync("some-other-token");

        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("CustomerSession.RefreshTokenInvalid");
    }

    [Fact]
    public async Task A_revoked_session_should_look_like_an_unknown_token()
    {
        var opened = Open();
        opened.Session.Revoke(Opened.AddHours(1));

        var result = await RefreshAsync(opened.RefreshToken);

        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("CustomerSession.RefreshTokenInvalid");
    }

    [Fact]
    public async Task An_expired_session_should_look_like_an_unknown_token()
    {
        var opened = Open();
        var hash = opened.Session.RefreshTokenHash;

        var result = await RefreshAsync(opened.RefreshToken, Opened.AddDays(31));

        result.Error.Code.ShouldBe("CustomerSession.RefreshTokenInvalid");
        opened.Session.RefreshTokenHash.ShouldBe(hash);
    }

    [Fact]
    public async Task A_refresh_token_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RefreshCustomerSessionValidator(), new RefreshCustomerSessionCommand("a-token"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_refresh_token_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RefreshCustomerSessionValidator(), new RefreshCustomerSessionCommand(null));

        problems.ShouldBe(["RefreshToken:Validation.Required"], ignoreOrder: true);
    }

    private OpenedSession Open()
    {
        var opened = CustomerSession.Open(CompanyId, CustomerId, TimeSpan.FromDays(30), Opened);
        _sessions.GetByRefreshTokenHashAsync(
                CompanyId, CustomerSession.HashToken(opened.RefreshToken), Arg.Any<CancellationToken>())
            .Returns(opened.Session);

        return opened;
    }

    private Task<Result<CustomerTokens>> RefreshAsync(string refreshToken, DateTime? nowUtc = null) =>
        new RefreshCustomerSessionHandler(
                _sessions, _tokens, BranchData.TenantOf(CompanyId), new FixedTimeProvider(nowUtc ?? Now))
            .HandleAsync(new RefreshCustomerSessionCommand(refreshToken), TestContext.Current.CancellationToken);
}