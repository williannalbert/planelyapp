using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Application.Scheduling.Appointments.ListByProfessional;
using PlanelyApp.Domain.Scheduling.Appointments;
using Shouldly;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class ListProfessionalAppointmentsTests
{
    private static readonly DateTime From = AvailabilityData.At(0);

    private readonly AgendaQueryScenario _scenario = new();

    [Fact]
    public async Task The_filter_should_have_the_professional_the_window_and_every_branch_for_staff_with_full_access()
    {
        await Handler().HandleAsync(
            Query() with { Status = AppointmentStatus.Pending, Page = 2, PageSize = 10 },
            TestContext.Current.CancellationToken);

        _scenario.Sent.ProfessionalId.ShouldBe(_scenario.Professional.Id);
        _scenario.Sent.FromUtc.ShouldBe(From);
        _scenario.Sent.ToUtc.ShouldBe(From.AddDays(7));
        _scenario.Sent.Status.ShouldBe(AppointmentStatus.Pending);
        _scenario.Sent.AllowedBranchIds.ShouldBeNull();
        await _scenario.ReadModel.Received(1).ListAsync(
            Arg.Any<AppointmentFilter>(), 2, 10, Arg.Any<CancellationToken>());
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
    public async Task What_the_read_model_lists_should_come_back_as_it_is()
    {
        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(_scenario.Page);
    }

    [Fact]
    public async Task A_professional_that_does_not_exist_should_be_not_found()
    {
        _scenario.Professionals = ProfessionalData.RepositoryWith(null);

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task A_customer_should_not_list_the_agenda_of_a_professional()
    {
        _scenario.ActAsCustomer();

        var result = await Handler().HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new ListProfessionalAppointmentsValidator(), Query());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_professional_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(), Query() with { ProfessionalId = Guid.Empty });

        problems.ShouldBe(["ProfessionalId:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task Missing_times_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(), Query() with { FromUtc = default, ToUtc = default });

        problems.ShouldBe(["FromUtc:Validation.Required", "ToUtc:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_time_that_is_not_utc_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(),
            Query() with { FromUtc = DateTime.SpecifyKind(From, DateTimeKind.Unspecified) });

        problems.ShouldBe(["FromUtc:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_end_that_is_not_after_the_start_should_be_reported_as_out_of_range()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(), Query() with { ToUtc = From });

        problems.ShouldBe(["ToUtc:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task More_than_thirty_one_days_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(), Query() with { ToUtc = From.AddDays(32) });

        problems.ShouldBe(["ToUtc:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task Thirty_one_days_themselves_should_be_accepted()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(), Query() with { ToUtc = From.AddDays(31) });

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_page_a_size_and_a_status_out_of_range_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new ListProfessionalAppointmentsValidator(),
            Query() with { Page = 0, PageSize = 101, Status = (AppointmentStatus)99 });

        problems.ShouldBe(
            ["Page:Validation.OutOfRange", "PageSize:Validation.OutOfRange", "Status:Validation.Invalid"],
            ignoreOrder: true);
    }

    private ListProfessionalAppointmentsQuery Query() => new(_scenario.Professional.Id, From, From.AddDays(7));

    private ListProfessionalAppointmentsHandler Handler() =>
        new(_scenario.Professionals, _scenario.Scopes(), _scenario.ReadModel);
}