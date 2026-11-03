using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CustomerData = PlanelyApp.Application.Tests.Organization.Customers.CustomerData;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

/// <summary>
/// A company with a branch, a professional, a customer and a user of the staff with access to every branch, and a
/// read model that remembers the filter it was asked with. The actor can be the staff or the customer.
/// </summary>
internal sealed class AgendaQueryScenario
{
    public AgendaQueryScenario()
    {
        Company = AvailabilityData.NewCompany();
        Branch = AvailabilityData.OpenBranch(Company.Id);
        Professional = AvailabilityData.WorkingProfessional(Company.Id, Branch, "Ana Souza");
        Customer = CustomerData.NewCustomer(Company.Id);
        Staff = UserData.NewUser(Company.Id);
        Staff.SetBranchAccess(true);

        Users = UserData.RepositoryWith(Staff);
        Branches = BranchData.RepositoryWith(Branch);
        Professionals = ProfessionalData.RepositoryWith(Professional);
        Customers = CustomerData.RepositoryWith(Customer);

        Page = new PagedResult<AppointmentSummaryDto>([], 1, PageLimits.DefaultPageSize, 0);
        ReadModel
            .ListAsync(
                Arg.Do<AppointmentFilter>(filter => Filter = filter),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Page);

        ActAsStaff();
    }

    public Company Company { get; }

    public Branch Branch { get; }

    public Professional Professional { get; }

    public Customer Customer { get; }

    public User Staff { get; }

    /// <summary>The repositories below are replaced by ones that find nothing in the tests that need it.</summary>
    public IUserRepository Users { get; set; }

    public IBranchRepository Branches { get; set; }

    public IProfessionalRepository Professionals { get; set; }

    public ICustomerRepository Customers { get; set; }

    public IAppointmentReadModel ReadModel { get; } = Substitute.For<IAppointmentReadModel>();

    public ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();

    /// <summary>What the read model answers.</summary>
    public PagedResult<AppointmentSummaryDto> Page { get; }

    private AppointmentFilter? Filter { get; set; }

    /// <summary>The filter the handler sent to the read model.</summary>
    public AppointmentFilter Sent => Filter ?? throw new InvalidOperationException("The read model was not asked.");

    public void ActAsStaff() => ActAs(ActorKind.Staff, Staff.Id);

    public void ActAsCustomer(Guid? customerId = null) => ActAs(ActorKind.Customer, customerId ?? Customer.Id);

    public BranchScopeResolver Scopes() => new(Users, Actor, BranchData.TenantOf(Company.Id));

    private void ActAs(ActorKind kind, Guid id)
    {
        Actor.Kind.Returns(kind);
        Actor.Id.Returns(id);
    }
}