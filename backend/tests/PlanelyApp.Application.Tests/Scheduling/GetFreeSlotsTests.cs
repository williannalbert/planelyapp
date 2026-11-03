using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Availability.GetFreeSlots;
using PlanelyApp.Application.Tests.Catalog;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class GetFreeSlotsTests
{
    private readonly Company _company = AvailabilityData.NewCompany();
    private readonly IProfessionalRepository _professionals = Substitute.For<IProfessionalRepository>();
    private readonly IAppointmentRepository _appointments = Substitute.For<IAppointmentRepository>();
    private readonly Guid _companyId;
    private readonly Branch _branch;
    private readonly ServiceOffering _service;
    private readonly List<ServiceOffering> _stored;
    private readonly Professional _ana;

    public GetFreeSlotsTests()
    {
        _companyId = _company.Id;
        _branch = AvailabilityData.OpenBranch(_companyId);
        _service = CatalogData.NewFixedService(_companyId);
        _stored = [_service];
        _ana = AvailabilityData.WorkingProfessional(_companyId, _branch, "Ana Souza", _service);
        Listed(_ana);
        Booked();
    }

    [Fact]
    public async Task A_free_day_should_offer_every_hour_of_the_working_period()
    {
        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Date.ShouldBe(AvailabilityData.Monday);
        var professional = result.Value.Professionals.Single();
        professional.ProfessionalId.ShouldBe(_ana.Id);
        professional.ProfessionalName.ShouldBe("Ana Souza");
        professional.ServicesDurationMinutes.ShouldBe(30);
        professional.StartsUtc.ShouldBe(AvailabilityData.EveryHour(9, 17), ignoreOrder: false);
    }

    [Fact]
    public async Task An_appointment_should_take_its_period_away()
    {
        Booked(NewAppointment(_ana, AvailabilityData.At(10), 60));

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        var expected = AvailabilityData.EveryHour(9, 17);
        expected.Remove(AvailabilityData.At(10));
        result.Value.Professionals.Single().StartsUtc.ShouldBe(expected, ignoreOrder: false);
    }

    [Fact]
    public async Task An_appointment_at_another_branch_should_also_take_the_travel_time_away()
    {
        // 13:00 to 13:30 elsewhere, with 30 minutes of travel before and after: 12:30 to 14:00 is blocked.
        Booked(NewAppointment(_ana, AvailabilityData.At(13), 30, branchId: Guid.NewGuid()));

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { StepMinutes = 30 }, TestContext.Current.CancellationToken);

        var expected = Enumerable.Range(9, 9)
            .SelectMany(hour => new[] { AvailabilityData.At(hour), AvailabilityData.At(hour, 30) })
            .Where(start => start != AvailabilityData.At(12, 30)
                            && start != AvailabilityData.At(13)
                            && start != AvailabilityData.At(13, 30))
            .ToList();
        result.Value.Professionals.Single().StartsUtc.ShouldBe(expected, ignoreOrder: false);
    }

    [Fact]
    public async Task Time_off_should_take_its_period_away()
    {
        _ana.AddTimeOff(DateTimeRange.Create(AvailabilityData.At(14), AvailabilityData.At(16)));

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        var expected = AvailabilityData.EveryHour(9, 17);
        expected.Remove(AvailabilityData.At(14));
        expected.Remove(AvailabilityData.At(15));
        result.Value.Professionals.Single().StartsUtc.ShouldBe(expected, ignoreOrder: false);
    }

    [Fact]
    public async Task The_interval_of_the_policy_should_make_each_slot_longer()
    {
        _company.UpdatePolicy(BookingPolicy.Create(2, 60, 24, 60, 30, false));

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        // 30 minutes of service plus 60 of interval: the last start that fits before 18:00 is 16:00.
        result.Value.Professionals.Single().StartsUtc.ShouldBe(AvailabilityData.EveryHour(9, 16), ignoreOrder: false);
    }

    [Fact]
    public async Task A_duration_of_the_professional_should_set_the_length()
    {
        _ana.EnableOffering(_service.Id, TimeSpan.FromMinutes(120));

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        var professional = result.Value.Professionals.Single();
        professional.ServicesDurationMinutes.ShouldBe(120);
        professional.StartsUtc.ShouldBe(AvailabilityData.EveryHour(9, 16), ignoreOrder: false);
    }

    [Fact]
    public async Task Several_services_should_add_up_their_durations_and_a_repeat_counts_twice()
    {
        var beard = CatalogData.NewFixedService(_companyId, "Beard", 30m);
        _stored.Add(beard);
        _ana.EnableOffering(beard.Id);

        var result = await Handler().HandleAsync(
            Query(_service.Id, beard.Id, _service.Id), TestContext.Current.CancellationToken);

        var professional = result.Value.Professionals.Single();
        professional.ServicesDurationMinutes.ShouldBe(90);
        professional.StartsUtc.ShouldBe(AvailabilityData.EveryHour(9, 16), ignoreOrder: false);
    }

    [Fact]
    public async Task Only_professionals_who_perform_every_service_should_be_offered_by_name()
    {
        var bruno = AvailabilityData.WorkingProfessional(_companyId, _branch, "Bruno Lima");
        var carla = AvailabilityData.WorkingProfessional(_companyId, _branch, "Carla Dias", _service);
        Listed(carla, bruno, _ana);

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        result.Value.Professionals.Select(professional => professional.ProfessionalName)
            .ShouldBe(["Ana Souza", "Carla Dias"], ignoreOrder: false);
    }

    [Fact]
    public async Task The_professional_asked_for_should_be_the_only_one_and_the_list_of_the_branch_unused()
    {
        var carla = AvailabilityData.WorkingProfessional(_companyId, _branch, "Carla Dias", _service);
        Listed(_ana, carla);
        _professionals.GetByIdAsync(carla.Id, Arg.Any<CancellationToken>()).Returns(carla);

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { ProfessionalId = carla.Id }, TestContext.Current.CancellationToken);

        result.Value.Professionals.Select(professional => professional.ProfessionalId).ShouldBe([carla.Id], ignoreOrder: false);
        await _professionals.DidNotReceive().ListActiveAtBranchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_professional_asked_for_who_does_not_work_at_the_branch_should_give_an_empty_list()
    {
        var outsider = ProfessionalData.NewProfessional(_companyId, "Davi");
        outsider.EnableOffering(_service.Id);
        _professionals.GetByIdAsync(outsider.Id, Arg.Any<CancellationToken>()).Returns(outsider);

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { ProfessionalId = outsider.Id }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Professionals.ShouldBeEmpty();
        await _appointments.DidNotReceive().ListOccupyingScheduleAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_professional_asked_for_who_does_not_perform_the_service_should_give_an_empty_list()
    {
        var bruno = AvailabilityData.WorkingProfessional(_companyId, _branch, "Bruno Lima");
        _professionals.GetByIdAsync(bruno.Id, Arg.Any<CancellationToken>()).Returns(bruno);

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { ProfessionalId = bruno.Id }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Professionals.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_professional_that_does_not_exist_should_be_not_found()
    {
        _professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Professional?)null);

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { ProfessionalId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(branches: BranchData.RepositoryWith(null)).HandleAsync(
            Query(_service.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(companies: CompanyData.RepositoryWith(null)).HandleAsync(
            Query(_service.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_not_found_and_look_for_no_professional()
    {
        var result = await Handler().HandleAsync(
            Query(_service.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        await _professionals.DidNotReceive().ListActiveAtBranchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_inactive_service_should_be_a_conflict()
    {
        _service.Deactivate();

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
    }

    [Fact]
    public async Task A_service_the_branch_does_not_offer_should_be_a_conflict()
    {
        _branch.MakeOfferingUnavailable(_service.Id);

        var result = await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
    }

    [Fact]
    public async Task The_minimum_notice_should_count_from_the_current_time()
    {
        // 09:00 at the branch; with 2 hours of notice the first start is 11:00.
        var result = await Handler(now: AvailabilityData.At(9)).HandleAsync(
            Query(_service.Id), TestContext.Current.CancellationToken);

        result.Value.Professionals.Single().StartsUtc.ShouldBe(AvailabilityData.EveryHour(11, 17), ignoreOrder: false);
    }

    [Fact]
    public async Task The_appointments_should_be_loaded_for_a_window_around_the_day_with_the_travel_time()
    {
        await Handler().HandleAsync(Query(_service.Id), TestContext.Current.CancellationToken);

        // The UTC day of the 22nd, 14 hours of margin and 30 minutes of travel on each side.
        await _appointments.Received(1).ListOccupyingScheduleAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == _ana.Id),
            new DateTime(2027, 11, 21, 9, 30, 0, DateTimeKind.Utc),
            new DateTime(2027, 11, 23, 14, 30, 0, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_day_the_branch_is_closed_should_give_the_professional_with_no_times()
    {
        var tuesday = new DateOnly(2027, 11, 23);

        var result = await Handler().HandleAsync(
            Query(_service.Id) with { Date = tuesday }, TestContext.Current.CancellationToken);

        result.Value.Professionals.Single().StartsUtc.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(), new GetFreeSlotsQuery(Guid.NewGuid(), AvailabilityData.Monday, [Guid.NewGuid()]));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_is_missing_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(), new GetFreeSlotsQuery(Guid.Empty, default, []));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "Date:Validation.Required", "ServiceIds:Validation.Required"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_service_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(),
            new GetFreeSlotsQuery(Guid.NewGuid(), AvailabilityData.Monday, [Guid.Empty]));

        problems.ShouldBe(["ServiceIds[0]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_professional_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(),
            new GetFreeSlotsQuery(Guid.NewGuid(), AvailabilityData.Monday, [Guid.NewGuid()], Guid.Empty));

        problems.ShouldBe(["ProfessionalId:Validation.Invalid"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public async Task A_step_out_of_range_should_be_reported(int step)
    {
        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(),
            new GetFreeSlotsQuery(Guid.NewGuid(), AvailabilityData.Monday, [Guid.NewGuid()], null, step));

        problems.ShouldBe(["StepMinutes:Validation.OutOfRange"], ignoreOrder: true);
    }

    [Fact]
    public async Task More_than_fifty_services_should_be_reported_as_invalid()
    {
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var problems = await TestValidation.RunAsync(
            new GetFreeSlotsValidator(), new GetFreeSlotsQuery(Guid.NewGuid(), AvailabilityData.Monday, ids));

        problems.ShouldBe(["ServiceIds:Validation.Invalid"], ignoreOrder: true);
    }

    private GetFreeSlotsQuery Query(params Guid[] serviceIds) =>
        new(_branch.Id, AvailabilityData.Monday, serviceIds, null, 60);

    private GetFreeSlotsHandler Handler(
        IBranchRepository? branches = null,
        ICompanyRepository? companies = null,
        DateTime? now = null) =>
        new(
            branches ?? BranchData.RepositoryWith(_branch),
            companies ?? CompanyData.RepositoryWith(_company),
            PromotionData.ServicesListing([.. _stored]),
            _professionals,
            _appointments,
            BranchData.TenantOf(_companyId),
            new FixedTimeProvider(now ?? AvailabilityData.Now));

    private void Listed(params Professional[] listed) =>
        _professionals.ListActiveAtBranchAsync(_branch.Id, Arg.Any<CancellationToken>()).Returns(listed);

    private void Booked(params Appointment[] booked) =>
        _appointments.ListOccupyingScheduleAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(booked);

    private Appointment NewAppointment(Professional professional, DateTime start, int minutes, Guid? branchId = null) =>
        Appointment.Create(
            _companyId,
            branchId ?? _branch.Id,
            Guid.NewGuid(),
            professional.Id,
            start,
            [new NewAppointmentItem(_service, TimeSpan.FromMinutes(minutes), PriceQuote.Fixed(_service.BasePrice!))],
            BookingPolicy.Default,
            AvailabilityData.Now,
            Guid.NewGuid());
}