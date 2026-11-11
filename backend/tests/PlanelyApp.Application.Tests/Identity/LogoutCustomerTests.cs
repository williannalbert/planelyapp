using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Identity.Sessions.Logout;
using PlanelyApp.Application.Tests.Organization.Branches;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Identity.Sessions;
using Shouldly;

namespace PlanelyApp.Application.Tests.Identity;

public class LogoutCustomerTests
{
    private static readonly DateTime Opened = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Opened.AddDays(1);
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private readonly ICustomerSessionRepository _sessions = Substitute.For<ICustomerSessionRepository>();

    [Fact]
    public async Task Logging_out_should_revoke_the_session()
    {
        var opened = Open();

        var result = await LogoutAsync(opened.RefreshToken);

        result.IsSuccess.ShouldBeTrue();
        opened.Session.RevokedAtUtc.ShouldBe(Now);
        opened.Session.IsActive(Now).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_token_should_still_be_a_success()
    {
        Open();

        var result = await LogoutAsync("some-other-token");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Logging_out_twice_should_keep_the_first_time()
    {
        var opened = Open();
        opened.Session.Revoke(Opened.AddHours(1));

        var result = await LogoutAsync(opened.RefreshToken);

        result.IsSuccess.ShouldBeTrue();
        opened.Session.RevokedAtUtc.ShouldBe(Opened.AddHours(1));
    }

    [Fact]
    public async Task An_empty_refresh_token_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(new LogoutCustomerValidator(), new LogoutCustomerCommand(" "));

        problems.ShouldBe(["RefreshToken:Validation.Required"], ignoreOrder: true);
    }

    private OpenedSession Open()
    {
        var opened = CustomerSession.Open(CompanyId, Guid.CreateVersion7(), TimeSpan.FromDays(30), Opened);
        _sessions.GetByRefreshTokenHashAsync(
                CompanyId, CustomerSession.HashToken(opened.RefreshToken), Arg.Any<CancellationToken>())
            .Returns(opened.Session);

        return opened;
    }

    private Task<Result<Unit>> LogoutAsync(string refreshToken) =>
        new LogoutCustomerHandler(_sessions, BranchData.TenantOf(CompanyId), new FixedTimeProvider(Now))
            .HandleAsync(new LogoutCustomerCommand(refreshToken), TestContext.Current.CancellationToken);
}