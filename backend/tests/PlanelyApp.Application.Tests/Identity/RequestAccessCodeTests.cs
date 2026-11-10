using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Identity.AccessCodes.Request;
using PlanelyApp.Application.Tests.Organization.Branches;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Identity;

public class RequestAccessCodeTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private const string BiaEmail = "bia@example.com";
    private const string BiaPhone = "+551133334444";

    private readonly IAccessCodeRepository _codes = Substitute.For<IAccessCodeRepository>();
    private readonly IAccessCodeSender _sender = Substitute.For<IAccessCodeSender>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public RequestAccessCodeTests() => Recent();

    [Fact]
    public async Task A_code_should_be_saved_hashed_and_sent_in_plain_text_once()
    {
        var deliveries = new List<AccessCodeDelivery>();
        var saved = new List<AccessCode>();
        _ = _sender.SendAsync(Arg.Do<AccessCodeDelivery>(deliveries.Add), Arg.Any<CancellationToken>());
        _codes.When(codes => codes.Add(Arg.Any<AccessCode>())).Do(call => saved.Add(call.Arg<AccessCode>()));

        var result = await Handler().HandleAsync(new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail), _ct);

        result.IsSuccess.ShouldBeTrue();
        var delivery = deliveries.Single();
        delivery.CompanyId.ShouldBe(CompanyId);
        delivery.Channel.ShouldBe(AccessChannel.Email);
        delivery.Destination.ShouldBe(BiaEmail);
        delivery.Code.Length.ShouldBe(AccessCode.CodeLength);
        delivery.ExpiresAtUtc.ShouldBe(Now + AccessCode.ValidFor);
        var accessCode = saved.Single();
        accessCode.CodeHash.ShouldNotBe(delivery.Code);
        accessCode.Verify(delivery.Code, Now).ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public async Task A_phone_should_be_sent_in_the_normalized_form()
    {
        var deliveries = new List<AccessCodeDelivery>();
        _ = _sender.SendAsync(Arg.Do<AccessCodeDelivery>(deliveries.Add), Arg.Any<CancellationToken>());

        await Handler().HandleAsync(new RequestAccessCodeCommand(AccessChannel.Phone, "+55 (11) 3333-4444"), _ct);

        deliveries.Single().Destination.ShouldBe(BiaPhone);
        deliveries.Single().Channel.ShouldBe(AccessChannel.Phone);
    }

    [Fact]
    public async Task The_codes_of_the_last_hour_should_be_the_ones_looked_up()
    {
        await Handler().HandleAsync(new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail), _ct);

        await _codes.Received(1).GetCreationTimesSinceAsync(
            CompanyId, BiaEmail, Now - AccessCode.ResendWindow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_second_code_within_a_minute_should_be_refused_and_nothing_sent()
    {
        Recent(Now.AddSeconds(-30));

        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail), _ct));

        exception.Code.ShouldBe("AccessCode.ResendTooSoon");
        _codes.DidNotReceive().Add(Arg.Any<AccessCode>());
        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, _ct);
    }

    [Fact]
    public async Task The_sixth_code_within_an_hour_should_be_refused_and_nothing_sent()
    {
        Recent(Now.AddMinutes(-2), Now.AddMinutes(-10), Now.AddMinutes(-20), Now.AddMinutes(-30), Now.AddMinutes(-40));

        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail), _ct));

        exception.Code.ShouldBe("AccessCode.ResendLimitReached");
        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, _ct);
    }

    [Fact]
    public async Task A_delivery_failure_should_reach_the_caller_so_nothing_is_saved()
    {
        _sender.SendAsync(Arg.Any<AccessCodeDelivery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AccessCodeDeliveryException("The provider is down.")));

        await Should.ThrowAsync<AccessCodeDeliveryException>(() => Handler().HandleAsync(
            new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail), _ct));
    }

    [Fact]
    public async Task A_valid_e_mail_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand(AccessChannel.Email, BiaEmail));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_phone_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand(AccessChannel.Phone, BiaPhone));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_invalid_e_mail_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand(AccessChannel.Email, "not-an-email"));

        problems.ShouldBe(["Destination:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_e_mail_sent_as_a_phone_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand(AccessChannel.Phone, BiaEmail));

        problems.ShouldBe(["Destination:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_destination_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand(AccessChannel.Email, null));

        problems.ShouldBe(["Destination:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_channel_that_does_not_exist_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new RequestAccessCodeValidator(), new RequestAccessCodeCommand((AccessChannel)9, BiaEmail));

        problems.ShouldBe(["Channel:Validation.Invalid"], ignoreOrder: true);
    }

    private RequestAccessCodeHandler Handler() =>
        new(_codes, _sender, BranchData.TenantOf(CompanyId), new FixedTimeProvider(Now));

    private void Recent(params DateTime[] createdAtUtc) =>
        _codes.GetCreationTimesSinceAsync(CompanyId, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<DateTime>)createdAtUtc);
}