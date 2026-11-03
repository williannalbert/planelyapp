using NSubstitute;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Application.Scheduling.Appointments.ListByBranch;
using PlanelyApp.Domain.Scheduling.Appointments;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class ListBranchAppointmentsTests
{
    private readonly AgendaQueryScenario _scenario = new();

    [Fact]
    public async Task The_days_of_the_branch_should_become_utc_limits()
    {
        // The branch is in São Paulo (UTC-3). Monday and Tuesday, both included: from Monday 00:00 to Wednesday 00:00.
        var query = Query() with { ToDate = AvailabilityData.Monday.AddDays(1) };

        await Handler().HandleAsync(query, TestContext.Current.CancellationToken);

        _scenario.Sent.BranchId.ShouldBe(_scenario.Branch.Id);
        _scenario.Sent.FromUtc.ShouldBe(AvailabilityData.At(0));
        _scenario.Sent.ToUtc.ShouldBe(AvailabilityData.At(0).AddDays(2));
        _scenario.Sent.AllowedBranchIds.ShouldBeNull();
    }

    [Fact]
    public async Task One_day_should_be_a_range_of_twenty_four_hours()
    {
        await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        (_scenario.Sent.ToUtc - _scenario.Sent.FromUtc).ShouldBe(TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task The_professional_the_status_and_the_page_should_reach_the_read_model()
    {
        var professional = Guid.NewGuid();

        await Handler().HandleAsync(
            Query() with
            {
                ProfessionalId = professional,
                Status = AppointmentStatus.Confirmed,
                Page = 3,
                PageSize = 50,
            },
            TestContext.Current.CancellationToken);

        _scenario.Sent.ProfessionalId.ShouldBe(professional);
        _scenario.Sent.Status.ShouldBe(AppointmentStatus.Confirmed);
        await _scenario.ReadModel.Received(1).ListAsync(
            Arg.Any<AppointmentFilter>(), 3, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(_scenario.Page);
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found()
    {
        _scenario.Branches = BranchData.RepositoryWith(null);

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task Staff_without_access_to_the_branch_should_be_forbidden()
    {
        _scenario.Staff.SetBranchAccess(false, [Guid.NewGuid()]);

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
        await _scenario.ReadModel.DidNotReceive().ListAsync(
            Arg.Any<AppointmentFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Staff_with_access_only_to_that_branch_should_list_it()
    {
        _scenario.Staff.SetBranchAccess(false, [_scenario.Branch.Id]);

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_customer_should_not_list_the_agenda_of_a_branch()
    {
        _scenario.ActAsCustomer();

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
        await _scenario.ReadModel.DidNotReceive().ListAsync(
            Arg.Any<AppointmentFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListBranchAppointmentsValidator(), Query());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_branch_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(), Query() with { BranchId = Guid.Empty });

        problems.ShouldBe(["BranchId:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task Missing_dates_should_be_reported_as_out_of_range()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(), Query() with { FromDate = default, ToDate = default });

        problems.ShouldBe(["FromDate:Validation.OutOfRange", "ToDate:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_end_before_the_start_should_be_reported_as_out_of_range()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(),
            Query() with { ToDate = AvailabilityData.Monday.AddDays(-1) });

        problems.ShouldBe(["ToDate:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task More_than_thirty_one_days_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(),
            Query() with { ToDate = AvailabilityData.Monday.AddDays(31) });

        problems.ShouldBe(["ToDate:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task Thirty_one_days_themselves_should_be_accepted()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(),
            Query() with { ToDate = AvailabilityData.Monday.AddDays(30) });

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_and_a_size_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(), Query() with { Page = 0, PageSize = 101 });

        problems.ShouldBe(["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_status_outside_the_enum_and_an_empty_professional_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListBranchAppointmentsValidator(),
            Query() with { Status = (AppointmentStatus)99, ProfessionalId = Guid.Empty });

        problems.ShouldBe(["Status:Validation.Invalid", "ProfessionalId:Validation.Invalid"], ignoreOrder: true);
    }

    private ListBranchAppointmentsQuery Query() =>
        new(_scenario.Branch.Id, AvailabilityData.Monday, AvailabilityData.Monday);

    private ListBranchAppointmentsHandler Handler() =>
        new(_scenario.Branches, _scenario.Scopes(), _scenario.ReadModel);
}