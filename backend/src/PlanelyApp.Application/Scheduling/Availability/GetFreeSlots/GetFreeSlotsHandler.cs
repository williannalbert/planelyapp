using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Availability;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Availability.GetFreeSlots;

internal sealed class GetFreeSlotsHandler(
    IBranchRepository branches,
    ICompanyRepository companies,
    IServiceOfferingRepository services,
    IProfessionalRepository professionals,
    IAppointmentRepository appointments,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : IQueryHandler<GetFreeSlotsQuery, FreeSlotsDto>
{
    // No time zone is more than 14 hours from UTC, so the local day is always inside this margin around the UTC day.
    private static readonly TimeSpan MaxUtcOffset = TimeSpan.FromHours(14);

    public async Task<Result<FreeSlotsDto>> HandleAsync(GetFreeSlotsQuery query, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(query.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        var company = await companies.GetByIdAsync(tenantProvider.RequireCompanyId(), cancellationToken);

        if (company is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        // A missing id and an id of another company look the same: the repository hides the other companies.
        var serviceIds = query.ServiceIds.Distinct().ToList();
        var found = (await services.ListByIdsAsync(serviceIds, cancellationToken)).ToDictionary(service => service.Id);

        if (found.Count != serviceIds.Count)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        if (found.Values.Any(service => !service.IsActive || !branch.Offers(service.Id)))
        {
            return Error.Conflict("ServiceOffering.NotAvailableInBranch");
        }

        IReadOnlyList<Professional> candidates;

        if (query.ProfessionalId is { } professionalId)
        {
            var professional = await professionals.GetByIdAsync(professionalId, cancellationToken);

            if (professional is null)
            {
                return Error.NotFound("Professional.NotFound");
            }

            candidates = [professional];
        }
        else
        {
            candidates = await professionals.ListActiveAtBranchAsync(branch.Id, cancellationToken);
        }

        var eligible = candidates
            .Where(professional => professional.IsActive
                                   && professional.WorksAt(branch.Id)
                                   && serviceIds.All(professional.CanPerform))
            .OrderBy(professional => professional.Name)
            .ThenBy(professional => professional.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            return new FreeSlotsDto(query.Date, []);
        }

        var policy = company.BookingPolicy;
        var travel = TimeSpan.FromMinutes(policy.TravelTimeBetweenBranchesMinutes);
        var dayStart = query.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Appointments at other branches also block the travel time, so the window goes beyond the day by that much.
        var booked = await appointments.ListOccupyingScheduleAsync(
            eligible.Select(professional => professional.Id).ToList(),
            dayStart - MaxUtcOffset - travel,
            dayStart.AddDays(1) + MaxUtcOffset + travel,
            cancellationToken);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var interval = TimeSpan.FromMinutes(policy.IntervalBetweenAppointmentsMinutes);
        var step = TimeSpan.FromMinutes(query.StepMinutes);

        var result = eligible
            .Select(professional =>
            {
                // CanPerform guarantees the offering. The professional may have a duration of their own.
                var duration = query.ServiceIds.Aggregate(
                    TimeSpan.Zero,
                    (total, id) => total + professional.GetOffering(id)!.ResolveDuration(found[id].StandardDuration));

                var slots = AvailabilityCalculator.GetSlots(
                    branch, professional, query.Date, booked, duration + interval, step, policy, nowUtc);

                return new ProfessionalSlotsDto(professional.Id, professional.Name, (int)duration.TotalMinutes, slots);
            })
            .ToList();

        return new FreeSlotsDto(query.Date, result);
    }
}