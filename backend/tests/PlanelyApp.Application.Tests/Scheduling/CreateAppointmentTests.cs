using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Create;
using PlanelyApp.Application.Tests.Catalog;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;
using CustomerData = PlanelyApp.Application.Tests.Organization.Customers.CustomerData;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class CreateAppointmentTests
{
    private readonly Company _company = AvailabilityData.NewCompany();
    private readonly IProfessionalRepository _professionals = Substitute.For<IProfessionalRepository>();
    private readonly IPromotionRepository _promotions = Substitute.For<IPromotionRepository>();
    private readonly IAppointmentRepository _appointments = Substitute.For<IAppointmentRepository>();
    private readonly ICurrentUser _actor = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _users;
    private readonly Guid _companyId;
    private readonly Branch _branch;
    private readonly ServiceOffering _service;
    private readonly List<ServiceOffering> _stored;
    private readonly Professional _ana;
    private readonly Customer _customer;
    private readonly User _staff;
    private Appointment? _added;

    public CreateAppointmentTests()
    {
        _companyId = _company.Id;
        _branch = AvailabilityData.OpenBranch(_companyId);
        _service = CatalogData.NewFixedService(_companyId);
        _stored = [_service];
        _ana = AvailabilityData.WorkingProfessional(_companyId, _branch, "Ana Souza", _service);
        _customer = CustomerData.NewCustomer(_companyId);
        _staff = UserData.NewUser(_companyId);
        _staff.SetBranchAccess(true);
        _users = UserData.RepositoryWith(_staff);

        _professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ana);
        InForce();
        Booked();
        ActAs(ActorKind.Staff, _staff.Id);
        _appointments.When(repository => repository.Add(Arg.Any<Appointment>()))
            .Do(call => _added = call.Arg<Appointment>());
    }

    private Appointment Added => _added ?? throw new InvalidOperationException("No appointment was added.");

    // ---- The way it works ----

    [Fact]
    public async Task A_free_time_should_create_the_appointment_and_return_its_id()
    {
        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(Added.Id);
        Added.CompanyId.ShouldBe(_companyId);
        Added.BranchId.ShouldBe(_branch.Id);
        Added.CustomerId.ShouldBe(_customer.Id);
        Added.ProfessionalId.ShouldBe(_ana.Id);
        Added.Status.ShouldBe(AppointmentStatus.Confirmed);
        Added.Period.ShouldBe(DateTimeRange.Create(AvailabilityData.At(10), AvailabilityData.At(10, 30)));
        Added.TotalPrice.ShouldBe(Money.From(50m));
        Added.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_schedule_version_of_the_professional_should_go_up_by_one()
    {
        var before = _ana.ScheduleVersion;

        await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        _ana.ScheduleVersion.ShouldBe(before + 1);
    }

    [Fact]
    public async Task The_notes_should_be_kept()
    {
        await Handler().HandleAsync(
            Booking() with { Notes = "Allergic to dye" }, TestContext.Current.CancellationToken);

        Added.Notes.ShouldBe("Allergic to dye");
    }

    [Fact]
    public async Task A_policy_that_asks_for_confirmation_should_leave_the_appointment_pending()
    {
        _company.UpdatePolicy(BookingPolicy.Create(2, 60, 24, 0, 30, true));

        await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        Added.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task A_service_on_request_should_leave_it_pending_without_a_total()
    {
        var tattoo = CatalogData.NewOnRequestService(_companyId);
        _stored.Add(tattoo);
        _ana.EnableOffering(tattoo.Id);

        var result = await Handler().HandleAsync(
            Booking() with { ServiceIds = [tattoo.Id] }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        Added.Status.ShouldBe(AppointmentStatus.Pending);
        Added.TotalPrice.ShouldBeNull();
        Added.HasPendingPrice.ShouldBeTrue();
    }

    [Fact]
    public async Task The_duration_of_the_professional_should_set_the_period()
    {
        _ana.EnableOffering(_service.Id, TimeSpan.FromMinutes(90));

        await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        Added.Period.End.ShouldBe(AvailabilityData.At(11, 30));
    }

    [Fact]
    public async Task A_bundle_should_discount_the_services_together()
    {
        var beard = CatalogData.NewFixedService(_companyId, "Beard", 30m);
        _stored.Add(beard);
        _ana.EnableOffering(beard.Id);
        InForce(PromotionData.NewBundleOf(_companyId, _service, beard));

        var result = await Handler().HandleAsync(
            Booking() with { ServiceIds = [_service.Id, beard.Id] }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        Added.TotalPrice.ShouldBe(Money.From(72m)); // 80 less 10%
        Added.Items.ShouldAllBe(item => item.IsFromBundle);
        Added.Period.End.ShouldBe(AvailabilityData.At(11));
    }

    [Fact]
    public async Task A_promotion_of_the_service_should_discount_the_total()
    {
        InForce(PromotionData.NewPromotionOf(_companyId, _service));

        await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        Added.TotalPrice.ShouldBe(Money.From(45m));
        Added.Items.Single().PromotionId.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_promotions_should_be_judged_on_the_local_day_of_the_branch()
    {
        // 01:30 UTC of the 23rd is Monday 22:30 at the branch (UTC-3). The branch is closed then, so it ends in a
        // conflict, but the promotions were asked for the 22nd, not for the UTC day.
        var start = new DateTime(2027, 11, 23, 1, 30, 0, DateTimeKind.Utc);

        await Handler().HandleAsync(Booking(start), TestContext.Current.CancellationToken);

        await _promotions.Received(1).ListInForceAsync(new DateOnly(2027, 11, 22), Arg.Any<CancellationToken>());
    }

    // ---- Who is acting ----

    [Fact]
    public async Task A_customer_should_book_for_themselves_without_looking_for_a_user()
    {
        ActAs(ActorKind.Customer, _customer.Id);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _users.DidNotReceive().GetByIdInCompanyAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_customer_booking_for_another_customer_should_be_forbidden()
    {
        ActAs(ActorKind.Customer, Guid.NewGuid());

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.NotTheCustomer");
        _added.ShouldBeNull();
    }

    [Fact]
    public async Task Staff_without_access_to_the_branch_should_be_forbidden()
    {
        _staff.SetBranchAccess(false, [Guid.NewGuid()]);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
        _added.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_who_is_not_of_the_company_should_be_forbidden()
    {
        var result = await Handler(users: UserData.RepositoryWith(null)).HandleAsync(
            Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task Staff_with_access_to_this_branch_only_should_book()
    {
        _staff.SetBranchAccess(false, [_branch.Id]);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    // ---- What it needs and what state it is in ----

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(branches: BranchData.RepositoryWith(null)).HandleAsync(
            Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(companies: CompanyData.RepositoryWith(null)).HandleAsync(
            Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task An_inactive_company_should_be_a_conflict()
    {
        _company.Deactivate();

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Company.Inactive");
    }

    [Fact]
    public async Task An_inactive_branch_should_be_a_conflict()
    {
        _branch.Deactivate();

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Branch.Inactive");
    }

    [Fact]
    public async Task A_customer_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(customers: CustomerData.RepositoryWith(null)).HandleAsync(
            Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
    }

    [Fact]
    public async Task A_customer_without_a_name_should_be_a_conflict()
    {
        var nameless = CustomerData.NewCustomer(_companyId, name: null);

        var result = await Handler(customers: CustomerData.RepositoryWith(nameless)).HandleAsync(
            Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Customer.NameRequired");
        _added.ShouldBeNull();
    }

    [Fact]
    public async Task A_professional_that_does_not_exist_should_be_not_found()
    {
        _professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Professional?)null);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task An_inactive_professional_should_be_a_conflict()
    {
        _ana.Deactivate();

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Professional.Inactive");
    }

    [Fact]
    public async Task A_professional_who_does_not_work_at_the_branch_should_be_a_conflict()
    {
        var davi = ProfessionalData.NewProfessional(_companyId, "Davi Reis");
        davi.EnableOffering(_service.Id);
        _professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(davi);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Professional.NotAtBranch");
    }

    [Fact]
    public async Task A_professional_who_does_not_perform_the_service_should_be_a_conflict()
    {
        var bruno = AvailabilityData.WorkingProfessional(_companyId, _branch, "Bruno Lima");
        _professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(bruno);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Professional.CannotPerformService");
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler().HandleAsync(
            Booking() with { ServiceIds = [_service.Id, Guid.NewGuid()] }, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task An_inactive_service_should_be_a_conflict()
    {
        _service.Deactivate();

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
    }

    [Fact]
    public async Task A_service_the_branch_does_not_offer_should_be_a_conflict()
    {
        _branch.MakeOfferingUnavailable(_service.Id);

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
    }

    // ---- The agenda ----

    [Fact]
    public async Task A_time_taken_by_another_appointment_should_be_a_conflict_and_leave_the_agenda_alone()
    {
        Booked(NewAppointment(AvailabilityData.At(10), 30));
        var before = _ana.ScheduleVersion;

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        _added.ShouldBeNull();
        _ana.ScheduleVersion.ShouldBe(before);
    }

    [Fact]
    public async Task The_travel_time_from_another_branch_should_make_it_a_conflict()
    {
        // 10:30 to 11:00 elsewhere, with 30 minutes of travel before it: 10:00 to 10:30 does not fit.
        Booked(NewAppointment(AvailabilityData.At(10, 30), 30, branchId: Guid.NewGuid()));

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
    }

    [Fact]
    public async Task Time_off_should_make_it_a_conflict()
    {
        _ana.AddTimeOff(DateTimeRange.Create(AvailabilityData.At(10), AvailabilityData.At(11)));

        var result = await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
    }

    [Fact]
    public async Task A_period_that_goes_beyond_the_working_hours_should_be_a_conflict()
    {
        // 17:45 to 18:15 and the day ends at 18:00.
        var result = await Handler().HandleAsync(
            Booking(AvailabilityData.At(17, 45)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
    }

    [Fact]
    public async Task The_appointments_should_be_loaded_for_the_period_with_the_travel_time_on_each_side()
    {
        await Handler().HandleAsync(Booking(), TestContext.Current.CancellationToken);

        await _appointments.Received(1).ListOccupyingScheduleAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == _ana.Id),
            AvailabilityData.At(10).AddMinutes(-30),
            AvailabilityData.At(10, 30).AddMinutes(30),
            Arg.Any<CancellationToken>());
    }

    // ---- The rules of the Domain come through ----

    [Fact]
    public async Task Less_notice_than_the_policy_asks_should_be_refused_by_the_Domain()
    {
        // Now is 09:00 on the day and the policy asks for 2 hours: the first start would be 11:00.
        var exception = await Should.ThrowAsync<DomainException>(
            () => Handler(now: AvailabilityData.At(9)).HandleAsync(Booking(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.MinimumNoticeNotMet");
        _added.ShouldBeNull();
    }

    [Fact]
    public async Task A_start_that_is_not_UTC_should_be_refused_by_the_Domain()
    {
        var notUtc = DateTime.SpecifyKind(AvailabilityData.At(10), DateTimeKind.Unspecified);

        var exception = await Should.ThrowAsync<DomainException>(
            () => Handler().HandleAsync(Booking(notUtc), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.TimeMustBeUtc");
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(new CreateAppointmentValidator(), Booking());

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_is_missing_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreateAppointmentValidator(),
            new CreateAppointmentCommand(Guid.Empty, Guid.Empty, Guid.Empty, default, []));

        problems.ShouldBe(
            [
                "BranchId:Validation.Required",
                "CustomerId:Validation.Required",
                "ProfessionalId:Validation.Required",
                "StartUtc:Validation.Required",
                "ServiceIds:Validation.Required",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_service_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreateAppointmentValidator(), Booking() with { ServiceIds = [Guid.Empty] });

        problems.ShouldBe(["ServiceIds[0]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task More_than_fifty_services_should_be_reported_as_invalid()
    {
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var problems = await TestValidation.RunAsync(
            new CreateAppointmentValidator(), Booking() with { ServiceIds = ids });

        problems.ShouldBe(["ServiceIds:Validation.Invalid"], ignoreOrder: true);
    }

    // ---- Helpers ----

    private CreateAppointmentCommand Booking(DateTime? startUtc = null) =>
        new(_branch.Id, _customer.Id, _ana.Id, startUtc ?? AvailabilityData.At(10), [_service.Id]);

    private CreateAppointmentHandler Handler(
        IBranchRepository? branches = null,
        ICompanyRepository? companies = null,
        ICustomerRepository? customers = null,
        IUserRepository? users = null,
        DateTime? now = null) =>
        new(
            branches ?? BranchData.RepositoryWith(_branch),
            companies ?? CompanyData.RepositoryWith(_company),
            customers ?? CustomerData.RepositoryWith(_customer),
            _professionals,
            Services(),
            _promotions,
            _appointments,
            users ?? _users,
            _actor,
            BranchData.TenantOf(_companyId),
            new FixedTimeProvider(now ?? AvailabilityData.Now));

    private void ActAs(ActorKind kind, Guid id)
    {
        _actor.Kind.Returns(kind);
        _actor.Id.Returns(id);
    }

    private void InForce(params Promotion[] inForce) =>
        _promotions.ListInForceAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(inForce);

    private void Booked(params Appointment[] booked) =>
        _appointments.ListOccupyingScheduleAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(booked);

    private Appointment NewAppointment(DateTime start, int minutes, Guid? branchId = null) =>
        Appointment.Create(
            _companyId,
            branchId ?? _branch.Id,
            Guid.NewGuid(),
            _ana.Id,
            start,
            [new NewAppointmentItem(_service, TimeSpan.FromMinutes(minutes), PriceQuote.Fixed(_service.BasePrice!))],
            BookingPolicy.Default,
            AvailabilityData.Now,
            Guid.NewGuid());

    private IServiceOfferingRepository Services()
    {
        var repository = Substitute.For<IServiceOfferingRepository>();
        repository.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ids = call.Arg<IReadOnlyCollection<Guid>>();

                return (IReadOnlyList<ServiceOffering>)_stored.Where(service => ids.Contains(service.Id)).ToList();
            });

        return repository;
    }
}