using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Companies;

public class BookingPolicyTests
{
    private static BookingPolicy Create(
        int minimumNoticeHours = 2,
        int maximumWindowDays = 60,
        int cancellationDeadlineHours = 24,
        int intervalBetweenAppointmentsMinutes = 0,
        int travelTimeBetweenBranchesMinutes = 30,
        bool requiresConfirmation = false) =>
        BookingPolicy.Create(
            minimumNoticeHours,
            maximumWindowDays,
            cancellationDeadlineHours,
            intervalBetweenAppointmentsMinutes,
            travelTimeBetweenBranchesMinutes,
            requiresConfirmation);

    [Fact]
    public void Default_policy_should_have_the_documented_values()
    {
        var policy = BookingPolicy.Default;

        policy.MinimumNoticeHours.ShouldBe(2);
        policy.MaximumWindowDays.ShouldBe(60);
        policy.CancellationDeadlineHours.ShouldBe(24);
        policy.IntervalBetweenAppointmentsMinutes.ShouldBe(0);
        policy.TravelTimeBetweenBranchesMinutes.ShouldBe(30);
        policy.RequiresConfirmation.ShouldBeFalse();
    }

    [Fact]
    public void Should_expose_the_given_values()
    {
        var policy = Create(
            minimumNoticeHours: 4,
            maximumWindowDays: 30,
            cancellationDeadlineHours: 12,
            intervalBetweenAppointmentsMinutes: 15,
            travelTimeBetweenBranchesMinutes: 45,
            requiresConfirmation: true);

        policy.MinimumNoticeHours.ShouldBe(4);
        policy.MaximumWindowDays.ShouldBe(30);
        policy.CancellationDeadlineHours.ShouldBe(12);
        policy.IntervalBetweenAppointmentsMinutes.ShouldBe(15);
        policy.TravelTimeBetweenBranchesMinutes.ShouldBe(45);
        policy.RequiresConfirmation.ShouldBeTrue();
    }

    [Fact]
    public void Zero_values_should_be_accepted_where_allowed()
    {
        Should.NotThrow(() => Create(
            minimumNoticeHours: 0,
            cancellationDeadlineHours: 0,
            intervalBetweenAppointmentsMinutes: 0,
            travelTimeBetweenBranchesMinutes: 0));
    }

    [Fact]
    public void Negative_values_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Create(minimumNoticeHours: -1))
            .Code.ShouldBe("BookingPolicy.MinimumNoticeInvalid");
        Should.Throw<DomainException>(() => Create(cancellationDeadlineHours: -1))
            .Code.ShouldBe("BookingPolicy.CancellationDeadlineInvalid");
        Should.Throw<DomainException>(() => Create(intervalBetweenAppointmentsMinutes: -1))
            .Code.ShouldBe("BookingPolicy.IntervalInvalid");
        Should.Throw<DomainException>(() => Create(travelTimeBetweenBranchesMinutes: -1))
            .Code.ShouldBe("BookingPolicy.TravelTimeInvalid");
    }

    [Fact]
    public void Window_smaller_than_one_day_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Create(maximumWindowDays: 0))
            .Code.ShouldBe("BookingPolicy.MaximumWindowInvalid");
    }

    [Fact]
    public void Window_not_longer_than_the_notice_should_be_rejected()
    {
        // 1 day = 24h of window; a 24h notice would leave nothing to book.
        Should.Throw<DomainException>(() => Create(minimumNoticeHours: 24, maximumWindowDays: 1))
            .Code.ShouldBe("BookingPolicy.WindowSmallerThanNotice");

        Should.NotThrow(() => Create(minimumNoticeHours: 23, maximumWindowDays: 1));
    }

    [Fact]
    public void Policies_with_same_values_should_be_equal()
    {
        Create().ShouldBe(Create());
        Create(requiresConfirmation: true).ShouldNotBe(Create());
    }
}