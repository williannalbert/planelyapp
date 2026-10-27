using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class AccessCodeRepositoryTests(MySqlFixture mysql)
{
    // A whole hour: MySQL keeps microseconds and a DateTime keeps 100 nanoseconds, so this comes back exactly.
    private static readonly DateTime Now = DateTime.UtcNow.Date.AddHours(8);

    [Fact]
    public async Task A_saved_code_should_come_back_and_still_accept_the_right_code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();
        var issued = await IssueAsync(company.Id, AccessChannel.Email, email, Now, cancellationToken);

        var loaded = await LoadAsync(company.Id, issued.AccessCode.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Channel.ShouldBe(AccessChannel.Email);
        loaded.Destination.ShouldBe(Email.Create(email).Value);
        loaded.CodeHash.ShouldBe(issued.AccessCode.CodeHash);
        loaded.CodeHash.Length.ShouldBe(64);
        loaded.CreatedAtUtc.ShouldBe(Now);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.ExpiresAtUtc.ShouldBe(Now + AccessCode.ValidFor);
        loaded.Attempts.ShouldBe(0);
        loaded.UsedAtUtc.ShouldBeNull();

        // The hash uses the Id as salt: if the Id or the hash did not survive the round trip, this would fail.
        loaded.Verify(issued.PlainCode, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Verified);
    }

    [Fact]
    public async Task A_code_sent_by_phone_should_be_saved_with_the_normalized_number()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var phone = StaffData.NewPhone();
        var issued = await IssueAsync(company.Id, AccessChannel.Phone, phone.Value, Now, cancellationToken);

        var loaded = await LoadAsync(company.Id, issued.AccessCode.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Channel.ShouldBe(AccessChannel.Phone);
        loaded.Destination.ShouldBe(phone.Value);
    }

    [Fact]
    public async Task Wrong_attempts_should_be_saved_and_lock_the_code_even_for_the_right_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var issued = await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now, cancellationToken);
        var id = issued.AccessCode.Id;
        var at = Now.AddMinutes(1);

        for (var attempt = 0; attempt < AccessCode.MaxAttempts; attempt++)
        {
            (await VerifyAsync(company.Id, id, WrongCodeFor(issued.PlainCode), at, cancellationToken))
                .ShouldBe(AccessCodeVerification.Invalid);
        }

        (await VerifyAsync(company.Id, id, issued.PlainCode, at, cancellationToken))
            .ShouldBe(AccessCodeVerification.TooManyAttempts);

        var loaded = await LoadAsync(company.Id, id, cancellationToken);
        loaded!.Attempts.ShouldBe(AccessCode.MaxAttempts);
        loaded.UsedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task A_code_that_was_used_should_be_saved_as_used_and_not_work_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var issued = await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now, cancellationToken);
        var id = issued.AccessCode.Id;

        (await VerifyAsync(company.Id, id, issued.PlainCode, Now.AddMinutes(1), cancellationToken))
            .ShouldBe(AccessCodeVerification.Verified);
        (await VerifyAsync(company.Id, id, issued.PlainCode, Now.AddMinutes(2), cancellationToken))
            .ShouldBe(AccessCodeVerification.AlreadyUsed);

        var loaded = await LoadAsync(company.Id, id, cancellationToken);
        loaded!.UsedAtUtc.ShouldBe(Now.AddMinutes(1));
        loaded.UsedAtUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task The_latest_code_of_a_destination_should_be_found_inside_its_own_company_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();

        await IssueAsync(company.Id, AccessChannel.Email, email, Now, cancellationToken);
        var second = await IssueAsync(company.Id, AccessChannel.Email, email, Now.AddMinutes(2), cancellationToken);
        await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now.AddMinutes(3), cancellationToken);

        var destination = Email.Create(email).Value;

        await using var scope = mysql.CreateScope(company.Id);
        var repository = scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>();

        (await repository.GetLatestAsync(company.Id, destination, cancellationToken))!.Id.ShouldBe(second.AccessCode.Id);
        (await repository.GetLatestAsync(company.Id, Email.Create(StaffData.NewEmail()).Value, cancellationToken)).ShouldBeNull();
        (await repository.GetLatestAsync(other.Id, destination, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task The_creation_times_since_an_instant_should_come_back_in_utc()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();

        await IssueAsync(company.Id, AccessChannel.Email, email, Now, cancellationToken);
        await IssueAsync(company.Id, AccessChannel.Email, email, Now.AddMinutes(2), cancellationToken);
        await IssueAsync(company.Id, AccessChannel.Email, email, Now.AddMinutes(4), cancellationToken);

        await using var scope = mysql.CreateScope(company.Id);
        var times = await scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>()
            .GetCreationTimesSinceAsync(company.Id, Email.Create(email).Value, Now.AddMinutes(1), cancellationToken);

        times.Order().ShouldBe([Now.AddMinutes(2), Now.AddMinutes(4)]);
        times.ShouldAllBe(time => time.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task The_resend_limits_should_work_with_the_times_the_repository_returns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = StaffData.NewEmail();

        await IssueAsync(company.Id, AccessChannel.Email, email, Now, cancellationToken);

        // Less than a minute after the previous one (RN22).
        await Should.ThrowAsync<DomainException>(
            () => IssueAsync(company.Id, AccessChannel.Email, email, Now.AddSeconds(30), cancellationToken));

        // Five in the last hour is the limit (RN22): the first is already saved, four more fit.
        for (var minutes = 2; minutes <= 8; minutes += 2)
        {
            await IssueAsync(company.Id, AccessChannel.Email, email, Now.AddMinutes(minutes), cancellationToken);
        }

        await Should.ThrowAsync<DomainException>(
            () => IssueAsync(company.Id, AccessChannel.Email, email, Now.AddMinutes(10), cancellationToken));
    }

    [Fact]
    public async Task A_code_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var issued = await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now, cancellationToken);

        (await LoadAsync(company.Id, issued.AccessCode.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, issued.AccessCode.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, issued.AccessCode.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_simultaneous_wrong_attempts_should_not_both_be_counted_as_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var issued = await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now, cancellationToken);
        var id = issued.AccessCode.Id;
        var wrong = WrongCodeFor(issued.PlainCode);

        await using var first = mysql.CreateScope(company.Id);
        await using var second = mysql.CreateScope(company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);

        // Both read "0 attempts" and both got the verdict "wrong".
        seenByFirst!.Verify(wrong, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Invalid);
        seenBySecond!.Verify(wrong, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Invalid);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        // Without the token this would save "1 attempt" again, and the limit of 5 could be beaten in parallel.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        (await LoadAsync(company.Id, id, cancellationToken))!.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task The_same_code_should_be_accepted_once_even_if_entered_twice_at_the_same_time()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var issued = await IssueAsync(company.Id, AccessChannel.Email, StaffData.NewEmail(), Now, cancellationToken);
        var id = issued.AccessCode.Id;

        await using var first = mysql.CreateScope(company.Id);
        await using var second = mysql.CreateScope(company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);

        seenByFirst!.Verify(issued.PlainCode, Now.AddMinutes(1)).ShouldBe(AccessCodeVerification.Verified);
        seenBySecond!.Verify(issued.PlainCode, Now.AddMinutes(2)).ShouldBe(AccessCodeVerification.Verified);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        // Only one of the two requests can open a session with this code.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        (await LoadAsync(company.Id, id, cancellationToken))!.UsedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    // Does what the use case will do: loads the recent times, issues, saves.
    private async Task<IssuedAccessCode> IssueAsync(
        Guid companyId,
        AccessChannel channel,
        string destination,
        DateTime at,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        var repository = scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>();

        var normalized = channel == AccessChannel.Email
            ? Email.Create(destination).Value
            : Phone.Create(destination).Value;

        var recent = await repository.GetCreationTimesSinceAsync(companyId, normalized, at.AddHours(-1), cancellationToken);
        var issued = AccessCode.Issue(companyId, channel, destination, recent, at);

        repository.Add(issued.AccessCode);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        return issued;
    }

    private async Task<AccessCodeVerification> VerifyAsync(
        Guid companyId,
        Guid id,
        string code,
        DateTime at,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        var accessCode = await scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);

        var result = accessCode!.Verify(code, at);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<AccessCode?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IAccessCodeRepository>().GetByIdAsync(id, cancellationToken);
    }

    private static string WrongCodeFor(string code) => code == "000000" ? "000001" : "000000";
}