using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Application.Tests.Catalog;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

/// <summary>
/// A company, a branch open on Mondays from 9 to 18, a professional who works there, a user of the staff with access to
/// every branch, a customer and an actor that can be either of them. Appointments are born in the state each test needs,
/// at 10:00 on the Monday of <see cref="AvailabilityData"/> unless another start is given.
/// </summary>
internal sealed class AppointmentScenario
{
    private static readonly BookingPolicy AsksConfirmation = BookingPolicy.Create(2, 60, 24, 0, 30, true);

    public AppointmentScenario()
    {
        Company = AvailabilityData.NewCompany();
        Branch = AvailabilityData.OpenBranch(Company.Id);
        Professional = AvailabilityData.WorkingProfessional(Company.Id, Branch, "Ana Souza");
        Staff = UserData.NewUser(Company.Id);
        Staff.SetBranchAccess(true);

        Users = UserData.RepositoryWith(Staff);
        Branches = BranchData.RepositoryWith(Branch);
        Professionals = ProfessionalData.RepositoryWith(Professional);
        Companies = CompanyData.RepositoryWith(Company);

        Booked();
        ActAsStaff();
    }

    public Company Company { get; }

    public Branch Branch { get; }

    public Professional Professional { get; }

    public Guid BranchId => Branch.Id;

    public Guid CustomerId { get; } = Guid.NewGuid();

    public User Staff { get; }

    /// <summary>The repositories below are replaced by ones that find nothing in the tests that need it.</summary>
    public IUserRepository Users { get; set; }

    public IBranchRepository Branches { get; set; }

    public IProfessionalRepository Professionals { get; set; }

    public ICompanyRepository Companies { get; set; }

    public IAppointmentRepository Appointments { get; } = Substitute.For<IAppointmentRepository>();

    public ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();

    public ITenantProvider Tenant => BranchData.TenantOf(Company.Id);

    public void ActAsStaff() => ActAs(ActorKind.Staff, Staff.Id);

    /// <summary>The customer of the appointments, unless another id is given.</summary>
    public void ActAsCustomer(Guid? customerId = null) => ActAs(ActorKind.Customer, customerId ?? CustomerId);

    /// <summary>Pending, because the policy asks for confirmation. The price is known.</summary>
    public Appointment Pending(DateTime? startUtc = null) => NewAppointment(AsksConfirmation, false, startUtc);

    /// <summary>Pending, with a service on request (120 minutes) still without a price.</summary>
    public Appointment PendingWithoutPrice(DateTime? startUtc = null) =>
        NewAppointment(BookingPolicy.Default, true, startUtc);

    public Appointment Confirmed(DateTime? startUtc = null) => NewAppointment(BookingPolicy.Default, false, startUtc);

    public Appointment InProgress()
    {
        var appointment = Confirmed();
        appointment.Start(Staff.Id, AvailabilityData.Now);

        return appointment;
    }

    /// <summary>What the agenda of the professional already has, whatever the window asked for.</summary>
    public void Booked(params Appointment[] others) =>
        Appointments.ListOccupyingScheduleAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Appointment>)others);

    public AppointmentAccess Access(Appointment? appointment)
    {
        Appointments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appointment);

        return new AppointmentAccess(Appointments, Users, Actor, Tenant);
    }

    public AppointmentAgenda Agenda() => new(Branches, Professionals, Appointments);

    private void ActAs(ActorKind kind, Guid id)
    {
        Actor.Kind.Returns(kind);
        Actor.Id.Returns(id);
    }

    private Appointment NewAppointment(BookingPolicy policy, bool onRequest, DateTime? startUtc)
    {
        var item = onRequest
            ? new NewAppointmentItem(
                CatalogData.NewOnRequestService(Company.Id), TimeSpan.FromMinutes(120), PriceQuote.OnRequest())
            : new NewAppointmentItem(
                CatalogData.NewFixedService(Company.Id), TimeSpan.FromMinutes(30), PriceQuote.Fixed(Money.From(50m)));

        var appointment = Appointment.Create(
            Company.Id,
            BranchId,
            CustomerId,
            Professional.Id,
            startUtc ?? AvailabilityData.At(10),
            [item],
            policy,
            AvailabilityData.Now,
            Staff.Id);
        appointment.ClearDomainEvents();

        return appointment;
    }
}