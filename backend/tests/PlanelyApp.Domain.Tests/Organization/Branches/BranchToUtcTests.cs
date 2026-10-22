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
    public void ToUtc_should_use_the_summer_and_the_winter_offset()
    {
        var branch = CreateBranch("America/New_York");

        branch.ToUtc(new DateOnly(2026, 7, 1), new TimeOnly(10, 0))
            .ShouldBe(new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc));
        branch.ToUtc(new DateOnly(2026, 1, 15), new TimeOnly(10, 0))
            .ShouldBe(new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtc_should_move_a_skipped_local_time_to_the_instant_the_clocks_jumped()
    {
        // In New York the clocks went from 02:00 straight to 03:00 on 2026-03-08, at 07:00 UTC.
        var branch = CreateBranch("America/New_York");
        var jump = new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc);

        branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 0)).ShouldBe(jump);
        branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 30)).ShouldBe(jump);
        branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 59)).ShouldBe(jump);
    }

    [Fact]
    public void ToUtc_should_stay_exact_right_before_and_after_the_jump()
    {
        var branch = CreateBranch("America/New_York");

        branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(1, 59))
            .ShouldBe(new DateTime(2026, 3, 8, 6, 59, 0, DateTimeKind.Utc));
        branch.ToUtc(new DateOnly(2026, 3, 8), new TimeOnly(3, 0))
            .ShouldBe(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc));
    }
    [Fact]
    public void ToUtc_should_read_a_repeated_local_time_as_standard_time()
    {
        // On 2026-11-01 the clocks went back and 01:30 happened twice; standard time (UTC-5) is the later one.
        var utc = CreateBranch("America/New_York").ToUtc(new DateOnly(2026, 11, 1), new TimeOnly(1, 30));

        utc.ShouldBe(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc));
    }
}