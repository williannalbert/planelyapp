using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Availability;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Appointments.Create;

internal sealed class CreateAppointmentHandler(
    IBranchRepository branches,
    ICompanyRepository companies,
    ICustomerRepository customers,
    IProfessionalRepository professionals,
    IServiceOfferingRepository services,
    IPromotionRepository promotions,
    IAppointmentRepository appointments,
    IUserRepository users,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<CreateAppointmentCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateAppointmentCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();

        // A customer books only for themselves. It costs no query, so it comes first.
        if (currentUser.Kind == ActorKind.Customer && command.CustomerId != currentUser.Id)
        {
            return Error.Forbidden("Appointment.NotTheCustomer");
        }

        var branch = await branches.GetByIdAsync(command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        if (currentUser.Kind == ActorKind.Staff
            && !await StaffCanAccessAsync(companyId, branch.Id, cancellationToken))
        {
            return Error.Forbidden("Branch.AccessDenied");
        }

        var company = await companies.GetByIdAsync(companyId, cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        if (!company.IsActive)
        {
            return Error.Conflict("Company.Inactive");
        }

        if (!branch.IsActive)
        {
            return Error.Conflict("Branch.Inactive");
        }

        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound");
        }

        // The first access creates the customer with only a contact (RN24); the name is asked before booking.
        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            return Error.Conflict("Customer.NameRequired");
        }

        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        if (!professional.IsActive)
        {
            return Error.Conflict("Professional.Inactive");
        }

        if (!professional.WorksAt(branch.Id))
        {
            return Error.Conflict("Professional.NotAtBranch");
        }

        // A missing id and an id of another company look the same: the repository hides the other companies.
        var serviceIds = command.ServiceIds.Distinct().ToList();
        var found = (await services.ListByIdsAsync(serviceIds, cancellationToken)).ToDictionary(service => service.Id);

        if (found.Count != serviceIds.Count)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        if (found.Values.Any(service => !service.IsActive || !branch.Offers(service.Id)))
        {
            return Error.Conflict("ServiceOffering.NotAvailableInBranch");
        }

        if (!serviceIds.All(professional.CanPerform))
        {
            return Error.Conflict("Professional.CannotPerformService");
        }

        // The promotions are judged on the day AT THE BRANCH (RN15). The Domain refuses a time that is not UTC.
        var asked = command.ServiceIds.Select(id => found[id]).ToList();
        var inForce = await promotions.ListInForceAsync(branch.GetLocalDate(command.StartUtc), cancellationToken);
        var quotes = PriceCalculator.CalculateAll(asked, branch, command.StartUtc, inForce);

        // The professional may have a duration of their own for a service. CanPerform guarantees the offering.
        var items = asked
            .Zip(quotes, (service, quote) => new NewAppointmentItem(
                service,
                professional.GetOffering(service.Id)!.ResolveDuration(service.StandardDuration),
                quote))
            .ToList();

        var policy = company.BookingPolicy;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // The Domain checks notice, window, bundles and the status. Its rules (422) come before the clash (409).
        var appointment = Appointment.Create(
            companyId,
            branch.Id,
            customer.Id,
            professional.Id,
            command.StartUtc,
            items,
            policy,
            nowUtc,
            currentUser.Id,
            command.Notes);

        // Only what touches the period, with the travel time to and from other branches, can stop it from fitting.
        var travel = TimeSpan.FromMinutes(policy.TravelTimeBetweenBranchesMinutes);
        var booked = await appointments.ListOccupyingScheduleAsync(
            [professional.Id],
            appointment.Period.Start - travel,
            appointment.Period.End + travel,
            cancellationToken);

        if (!AvailabilityCalculator.IsAvailable(branch, professional, appointment.Period, booked, policy))
        {
            return Error.Conflict("Appointment.TimeNotAvailable");
        }

        // RN27: the version goes up in the same transaction as the appointment. If someone else booked this
        // professional after we read it, the save fails with DbUpdateConcurrencyException and nothing is kept.
        professional.IncrementScheduleVersion();
        appointments.Add(appointment);

        return appointment.Id;
    }

    // A user has no tenant filter, so the lookup names the company (RN23). Whether the user may use the branch is RN11.
    private async Task<bool> StaffCanAccessAsync(Guid companyId, Guid branchId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdInCompanyAsync(companyId, currentUser.Id, cancellationToken);

        return user is not null && user.CanAccess(branchId);
    }
}