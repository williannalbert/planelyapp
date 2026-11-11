using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Application.Scheduling.Appointments.ListByCustomer;
using PlanelyApp.Domain.Scheduling.Appointments;
using Shouldly;
using CustomerData = PlanelyApp.Application.Tests.Organization.Customers.CustomerData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class ListCustomerAppointmentsTests
{
    private readonly AgendaQueryScenario _scenario = new();

    [Fact]
    public async Task A_customer_should_list_their_own_history_newest_first_in_every_branch()
    {
        _scenario.ActAsCustomer();

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _scenario.Sent.CustomerId.ShouldBe(_scenario.Customer.Id);
        _scenario.Sent.AllowedBranchIds.ShouldBeNull();
        _scenario.Sent.UpcomingFromUtc.ShouldBeNull();
        _scenario.Sent.NewestFirst.ShouldBeTrue();
    }

    [Fact]
    public async Task A_customer_should_not_find_another_customer()
    {
        _scenario.ActAsCustomer(Guid.NewGuid());

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
        await _scenario.ReadModel.DidNotReceive().ListAsync(
            Arg.Any<AppointmentFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Staff_with_full_access_should_list_any_customer_in_every_branch()
    {
        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _scenario.Sent.CustomerId.ShouldBe(_scenario.Customer.Id);
        _scenario.Sent.AllowedBranchIds.ShouldBeNull();
    }

    [Fact]
    public async Task Staff_with_access_to_some_branches_should_see_only_those()
    {
        var allowed = Guid.NewGuid();
        _scenario.Staff.SetBranchAccess(false, [allowed]);

        await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        _scenario.Sent.AllowedBranchIds.ShouldNotBeNull();
        _scenario.Sent.AllowedBranchIds.ShouldBe([allowed], ignoreOrder: true);
    }

    [Fact]
    public async Task A_customer_that_does_not_exist_should_be_not_found_for_the_staff()
    {
        _scenario.Customers = CustomerData.RepositoryWith(null);

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Customer.NotFound");
    }

    [Fact]
    public async Task Only_the_upcoming_should_start_from_now_and_come_nearest_first()
    {
        _scenario.ActAsCustomer();
        var now = AvailabilityData.Now;

        await Handler(now).HandleAsync(Query() with { OnlyUpcoming = true }, TestContext.Current.CancellationToken);

        _scenario.Sent.UpcomingFromUtc.ShouldBe(now);
        _scenario.Sent.NewestFirst.ShouldBeFalse();
    }

    [Fact]
    public async Task The_status_and_the_page_should_reach_the_read_model()
    {
        await Handler().HandleAsync(
            Query() with { Status = AppointmentStatus.Cancelled, Page = 4, PageSize = 25 },
            TestContext.Current.CancellationToken);

        _scenario.Sent.Status.ShouldBe(AppointmentStatus.Cancelled);
        await _scenario.ReadModel.Received(1).ListAsync(
            Arg.Any<AppointmentFilter>(), 4, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(_scenario.Page);
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListCustomerAppointmentsValidator(), Query());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_customer_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ListCustomerAppointmentsValidator(), Query() with { CustomerId = Guid.Empty });

        problems.ShouldBe(["CustomerId:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_page_a_size_and_a_status_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListCustomerAppointmentsValidator(),
            Query() with { Page = 0, PageSize = 101, Status = (AppointmentStatus)99 });

        problems.ShouldBe(
            ["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange", "Status:Validation.Invalid"],
            ignoreOrder: true);
    }

    private ListCustomerAppointmentsQuery Query() => new(_scenario.Customer.Id);

    private ListCustomerAppointmentsHandler Handler(DateTime? now = null) =>
        new(
            _scenario.Customers,
            _scenario.Scopes(),
            _scenario.Actor,
            _scenario.ReadModel,
            new FixedTimeProvider(now ?? AvailabilityData.Now));
}