using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Identity.AccessCodes.Verify;
using PlanelyApp.Application.Identity.Sessions;
using PlanelyApp.Application.Tests.Organization.Branches;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Identity;

public class VerifyAccessCodeTests
{
    private static readonly DateTime Issued = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Issued.AddMinutes(1);
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private const string BiaEmail = "bia@example.com";
    private const string BiaPhone = "+551133334444";

    private readonly IAccessCodeRepository _codes = Substitute.For<IAccessCodeRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly ICustomerSessionRepository _sessions = Substitute.For<ICustomerSessionRepository>();
    private readonly ICustomerTokenIssuer _tokens = Substitute.For<ICustomerTokenIssuer>();
    private readonly List<Customer> _addedCustomers = [];
    private readonly List<CustomerSession> _addedSessions = [];

    public VerifyAccessCodeTests()
    {
        _tokens.Issue(CompanyId, Arg.Any<Guid>(), Arg.Any<DateTime>())
            .Returns(new CustomerAccessToken("access-token", Now.AddMinutes(15)));
        _customers.When(customers => customers.Add(Arg.Any<Customer>())).Do(call => _addedCustomers.Add(call.Arg<Customer>()));
        _sessions.When(sessions => sessions.Add(Arg.Any<CustomerSession>())).Do(call => _addedSessions.Add(call.Arg<CustomerSession>()));
    }

    [Fact]
    public async Task The_right_code_on_the_first_access_should_register_the_customer_with_only_the_contact_and_open_a_session()
    {
        var issued = Issue(AccessChannel.Email, BiaEmail);

        var result = await VerifyAsync(AccessChannel.Email, BiaEmail, issued.PlainCode);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Outcome.ShouldBe(AccessCodeVerification.Verified);
        var login = result.Value.Login.ShouldNotBeNull();
        login.IsNewCustomer.ShouldBeTrue();
        login.AccessToken.ShouldBe("access-token");

        var customer = _addedCustomers.Single();
        customer.Id.ShouldBe(login.CustomerId);
        customer.Name.ShouldBeNull();
        customer.Phone.ShouldBeNull();
        customer.Email.ShouldNotBeNull().Value.ShouldBe(BiaEmail);
        customer.LastAccessAt.ShouldBe(Now);

        var session = _addedSessions.Single();
        session.CustomerId.ShouldBe(customer.Id);
        session.CompanyId.ShouldBe(CompanyId);
        session.RefreshTokenHash.ShouldBe(CustomerSession.HashToken(login.RefreshToken));
        session.ExpiresAtUtc.ShouldBe(Now + new CustomerSessionOptions().SessionLifetime);
        login.SessionExpiresAtUtc.ShouldBe(session.ExpiresAtUtc);

        issued.AccessCode.UsedAtUtc.ShouldBe(Now);
        _tokens.Received(1).Issue(CompanyId, customer.Id, Now);
    }

    [Fact]
    public async Task The_right_code_of_a_known_phone_should_log_in_the_same_customer()
    {
        var issued = Issue(AccessChannel.Phone, BiaPhone);
        var existing = Customer.Register(CompanyId, null, Phone.Create(BiaPhone), "Bia");
        _customers.FindByPhoneAsync(CompanyId, Arg.Any<Phone>(), Arg.Any<CancellationToken>()).Returns(existing);

        var result = await VerifyAsync(AccessChannel.Phone, BiaPhone, issued.PlainCode);

        var login = result.Value.Login.ShouldNotBeNull();
        login.IsNewCustomer.ShouldBeFalse();
        login.CustomerId.ShouldBe(existing.Id);
        existing.LastAccessAt.ShouldBe(Now);
        _addedCustomers.ShouldBeEmpty();
        _addedSessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_wrong_code_should_be_a_success_that_counts_the_attempt_and_opens_nothing()
    {
        var issued = Issue(AccessChannel.Email, BiaEmail);

        var result = await VerifyAsync(AccessChannel.Email, BiaEmail, Wrong(issued));

        // A success on purpose: only a success is saved, and the attempt has to be.
        result.IsSuccess.ShouldBeTrue();
        result.Value.Outcome.ShouldBe(AccessCodeVerification.Invalid);
        result.Value.Login.ShouldBeNull();
        issued.AccessCode.Attempts.ShouldBe(1);
        _addedCustomers.ShouldBeEmpty();
        _addedSessions.ShouldBeEmpty();
        _tokens.DidNotReceiveWithAnyArgs().Issue(default, default, default);
    }

    [Fact]
    public async Task A_destination_without_a_code_should_look_like_a_wrong_code()
    {
        _codes.GetLatestAsync(CompanyId, BiaEmail, Arg.Any<CancellationToken>()).Returns((AccessCode?)null);

        var result = await VerifyAsync(AccessChannel.Email, BiaEmail, "123456");

        result.Value.Outcome.ShouldBe(AccessCodeVerification.Invalid);
        result.Value.Login.ShouldBeNull();
    }

    [Fact]
    public async Task An_expired_code_should_be_reported_as_expired()
    {
        var issued = Issue(AccessChannel.Email, BiaEmail);

        var result = await VerifyAsync(AccessChannel.Email, BiaEmail, issued.PlainCode, Issued + AccessCode.ValidFor);

        result.Value.Outcome.ShouldBe(AccessCodeVerification.Expired);
        _addedSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_code_should_work_only_once()
    {
        var issued = Issue(AccessChannel.Email, BiaEmail);

        await VerifyAsync(AccessChannel.Email, BiaEmail, issued.PlainCode);
        var second = await VerifyAsync(AccessChannel.Email, BiaEmail, issued.PlainCode);

        second.Value.Outcome.ShouldBe(AccessCodeVerification.AlreadyUsed);
        _addedSessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task After_five_wrong_codes_even_the_right_one_should_be_refused()
    {
        var issued = Issue(AccessChannel.Email, BiaEmail);

        for (var attempt = 0; attempt < AccessCode.MaxAttempts; attempt++)
        {
            (await VerifyAsync(AccessChannel.Email, BiaEmail, Wrong(issued))).Value.Outcome
                .ShouldBe(AccessCodeVerification.Invalid);
        }

        var result = await VerifyAsync(AccessChannel.Email, BiaEmail, issued.PlainCode);

        result.Value.Outcome.ShouldBe(AccessCodeVerification.TooManyAttempts);
        result.Value.Login.ShouldBeNull();
        issued.AccessCode.Attempts.ShouldBe(AccessCode.MaxAttempts);
        _addedSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new VerifyAccessCodeValidator(), new VerifyAccessCodeCommand(AccessChannel.Email, BiaEmail, "123456"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_code_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new VerifyAccessCodeValidator(), new VerifyAccessCodeCommand(AccessChannel.Email, BiaEmail, null));

        problems.ShouldBe(["Code:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_code_with_fewer_than_six_digits_should_be_a_400_that_costs_no_attempt()
    {
        var problems = await TestValidation.RunAsync(
            new VerifyAccessCodeValidator(), new VerifyAccessCodeCommand(AccessChannel.Email, BiaEmail, "12345"));

        problems.ShouldBe(["Code:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_code_with_a_letter_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new VerifyAccessCodeValidator(), new VerifyAccessCodeCommand(AccessChannel.Email, BiaEmail, "12345a"));

        problems.ShouldBe(["Code:Validation.Invalid"], ignoreOrder: true);
    }

    // The code the Domain issued for the destination, found as the latest one.
    private IssuedAccessCode Issue(AccessChannel channel, string destination)
    {
        var issued = AccessCode.Issue(CompanyId, channel, destination, [], Issued);
        _codes.GetLatestAsync(CompanyId, destination, Arg.Any<CancellationToken>()).Returns(issued.AccessCode);

        return issued;
    }

    private static string Wrong(IssuedAccessCode issued) => issued.PlainCode == "000000" ? "111111" : "000000";

    private Task<Result<VerifyAccessCodeResponse>> VerifyAsync(
        AccessChannel channel,
        string destination,
        string code,
        DateTime? nowUtc = null) =>
        new VerifyAccessCodeHandler(
                _codes,
                _customers,
                _sessions,
                _tokens,
                new CustomerSessionOptions(),
                BranchData.TenantOf(CompanyId),
                new FixedTimeProvider(nowUtc ?? Now))
            .HandleAsync(new VerifyAccessCodeCommand(channel, destination, code), TestContext.Current.CancellationToken);
}