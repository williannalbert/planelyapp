using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Branches;

public class BranchLocalDateTests
{
    private static Branch CreateBranch(string timeZoneId) =>
        Branch.Create(
            Guid.CreateVersion7(),
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            timeZoneId);

    [Fact]
    public void GetLocalDate_should_use_the_previous_day_when_the_branch_is_behind_UTC()
    {
        var branch = CreateBranch("America/Sao_Paulo");

        // 01:00 UTC on the 7th is 22:00 on the 6th in São Paulo (UTC-3).
        var date = branch.GetLocalDate(new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc));

        date.ShouldBe(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public void GetLocalDate_should_use_the_same_day_once_the_local_midnight_passes()
    {
        var branch = CreateBranch("America/Sao_Paulo");

        var date = branch.GetLocalDate(new DateTime(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc));

        date.ShouldBe(new DateOnly(2026, 11, 7));
    }

    [Fact]
    public void GetLocalDate_should_use_the_next_day_when_the_branch_is_ahead_of_UTC()
    {
        var branch = CreateBranch("Asia/Tokyo");

        // 20:00 UTC on the 6th is 05:00 on the 7th in Tokyo (UTC+9).
        var date = branch.GetLocalDate(new DateTime(2026, 11, 6, 20, 0, 0, DateTimeKind.Utc));

        date.ShouldBe(new DateOnly(2026, 11, 7));
    }

    [Fact]
    public void GetLocalDate_with_an_instant_that_is_not_UTC_should_be_rejected()
    {
        var branch = CreateBranch("America/Sao_Paulo");

        foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Local })
        {
            Should.Throw<DomainException>(() => branch.GetLocalDate(new DateTime(2026, 11, 7, 1, 0, 0, kind)))
                .Code.ShouldBe("Branch.InstantMustBeUtc");
        }
    }
}