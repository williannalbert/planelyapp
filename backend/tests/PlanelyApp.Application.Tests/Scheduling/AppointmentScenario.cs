using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Application.Tests.Catalog;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

/// <summary>
/// A company, a user of the staff with access to every branch, a customer and an actor that can be either of them.
/// Appointments are born in the state each test needs, on the Monday of <see cref="AvailabilityData"/>.
/// </summary>
internal sealed class AppointmentScenario
{
    private static readonly BookingPolicy AsksConfirmation = BookingPolicy.Create(2, 60, 24, 0, 30, true);

    public AppointmentScenario()
    {
        Company = AvailabilityData.NewCompany();
        Staff = UserData.NewUser(Company.Id);
        Staff.SetBranchAccess(true);
        Users = UserData.RepositoryWith(Staff);
        ActAsStaff();
    }

    public Company Company { get; }

    public Guid BranchId { get; } = Guid.NewGuid();

    public Guid CustomerId { get; } = Guid.NewGuid();

    public User Staff { get; }

    /// <summary>Replaced by a repository that finds nobody in the tests that need it.</summary>
    public IUserRepository Users { get; set; }

    public ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();

    public ITenantProvider Tenant => BranchData.TenantOf(Company.Id);

    public void ActAsStaff() => ActAs(ActorKind.Staff, Staff.Id);

    /// <summary>The customer of the appointments, unless another id is given.</summary>
    public void ActAsCustomer(Guid? customerId = null) => ActAs(ActorKind.Customer, customerId ?? CustomerId);

    /// <summary>Pending, because the policy asks for confirmation. The price is known.</summary>
    public Appointment Pending() => NewAppointment(AsksConfirmation, onRequest: false);

    /// <summary>Pending, with a service on request still without a price.</summary>
    public Appointment PendingWithoutPrice() => NewAppointment(BookingPolicy.Default, onRequest: true);

    public Appointment Confirmed() => NewAppointment(BookingPolicy.Default, onRequest: false);

    public Appointment InProgress()
    {
        var appointment = Confirmed();
        appointment.Start(Staff.Id, AvailabilityData.Now);

        return appointment;
    }

    public AppointmentAccess Access(Appointment? appointment) => new(Repository(appointment), Users, Actor, Tenant);

    public static IAppointmentRepository Repository(Appointment? appointment)
    {
        var appointments = Substitute.For<IAppointmentRepository>();
        appointments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appointment);

        return appointments;
    }

    private void ActAs(ActorKind kind, Guid id)
    {
        Actor.Kind.Returns(kind);
        Actor.Id.Returns(id);
    }

    private Appointment NewAppointment(BookingPolicy policy, bool onRequest)
    {
        var item = onRequest
            ? new NewAppointmentItem(
                CatalogData.NewOnRequestService(Company.Id), TimeSpan.FromMinutes(120), PriceQuote.OnRequest())
            : new NewAppointmentItem(
                CatalogData.NewFixedService(Company.Id), TimeSpan.FromMinutes(30), PriceQuote.Fixed(Money()));

        var appointment = Appointment.Create(
            Company.Id,
            BranchId,
            CustomerId,
            Guid.NewGuid(),
            AvailabilityData.At(10),
            [item],
            policy,
            AvailabilityData.Now,
            Staff.Id);
        appointment.ClearDomainEvents();

        return appointment;
    }

    private static Domain.SharedKernel.Money Money() => Domain.SharedKernel.Money.From(50m);
}