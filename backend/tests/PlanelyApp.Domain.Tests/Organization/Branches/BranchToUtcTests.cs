using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Branches;

public class BranchToUtcTests
{
    private static Branch CreateBranch(string timeZoneId) =>
        Branch.Create(
            Guid.CreateVersion7(),
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            timeZoneId);

    [Fact]
    public void ToUtc_should_add_three_hours_in_Sao_Paulo()
    {
        var utc = CreateBranch("America/Sao_Paulo").ToUtc(new DateOnly(2026, 11, 9), new TimeOnly(10, 0));

        utc.ShouldBe(new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc));
        utc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ToUtc_should_add_four_hours_in_Manaus()
    {
        var utc = CreateBranch("America/Manaus").ToUtc(new DateOnly(2026, 11, 9), new TimeOnly(10, 0));

        utc.ShouldBe(new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtc_should_move_to_the_next_day_when_the_offset_crosses_midnight()
    {
        var utc = CreateBranch("America/Sao_Paulo").ToUtc(new DateOnly(2026, 11, 9), new TimeOnly(22, 30));

        utc.ShouldBe(new DateTime(2026, 11, 10, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtc_should_reject_a_local_time_skipped_when_the_clocks_jump_forward()
    {
        // In New York the clocks went from 02:00 straight to 03:00 on 2026-03-08.
        var branch = CreateBranch("America/New_York");

        Should.Throw<DomainException>(() => branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 30)))
            .Code.ShouldBe("Branch.LocalTimeDoesNotExist");
    }

    [Fact]
    public void ToUtc_should_read_a_repeated_local_time_as_standard_time()
    {
        // On 2026-11-01 the clocks went back and 01:30 happened twice; standard time (UTC-5) is the later one.
        var utc = CreateBranch("America/New_York").ToUtc(new DateOnly(2026, 11, 1), new TimeOnly(1, 30));

        utc.ShouldBe(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc));
    }
}