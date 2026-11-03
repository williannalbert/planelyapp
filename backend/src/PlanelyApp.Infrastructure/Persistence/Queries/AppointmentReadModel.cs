using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class AppointmentReadModel(PlanelyDbContext context) : IAppointmentReadModel
{
    public async Task<PagedResult<AppointmentSummaryDto>> ListAsync(
        AppointmentFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var appointments = Apply(context.Set<Appointment>().AsNoTracking(), filter);

        var totalCount = await appointments.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PagedResult<AppointmentSummaryDto>([], page, pageSize, 0);
        }

        var ordered = filter.NewestFirst
            ? appointments.OrderByDescending(appointment => appointment.Period.Start).ThenBy(appointment => appointment.Id)
            : appointments.OrderBy(appointment => appointment.Period.Start).ThenBy(appointment => appointment.Id);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(appointment => new
            {
                appointment.Id,
                appointment.BranchId,
                appointment.CustomerId,
                appointment.ProfessionalId,
                Start = appointment.Period.Start,
                End = appointment.Period.End,
                appointment.Status,
                appointment.TotalPrice,
                Services = appointment.Items.Select(item => item.ServiceName).ToList(),
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new PagedResult<AppointmentSummaryDto>([], page, pageSize, totalCount);
        }

        // The names of the page, by id: no join, and the page is small.
        var branchIds = rows.Select(row => row.BranchId).Distinct().ToList();
        var customerIds = rows.Select(row => row.CustomerId).Distinct().ToList();
        var professionalIds = rows.Select(row => row.ProfessionalId).Distinct().ToList();

        var branchNames = await context.Set<Branch>()
            .AsNoTracking()
            .Where(branch => branchIds.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, branch => branch.Name, cancellationToken);
        var customerNames = await context.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customerIds.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, customer => customer.Name, cancellationToken);
        var professionalNames = await context.Set<Professional>()
            .AsNoTracking()
            .Where(professional => professionalIds.Contains(professional.Id))
            .ToDictionaryAsync(professional => professional.Id, professional => professional.Name, cancellationToken);

        var items = rows
            .Select(row => new AppointmentSummaryDto(
                row.Id,
                row.BranchId,
                branchNames.GetValueOrDefault(row.BranchId),
                row.CustomerId,
                customerNames.GetValueOrDefault(row.CustomerId),
                row.ProfessionalId,
                professionalNames.GetValueOrDefault(row.ProfessionalId),
                row.Start,
                row.End,
                row.Status,
                row.TotalPrice?.Amount,
                row.TotalPrice is null,
                row.Services))
            .ToList();

        return new PagedResult<AppointmentSummaryDto>(items, page, pageSize, totalCount);
    }

    private static IQueryable<Appointment> Apply(IQueryable<Appointment> appointments, AppointmentFilter filter)
    {
        if (filter.BranchId is { } branchId)
        {
            appointments = appointments.Where(appointment => appointment.BranchId == branchId);
        }

        if (filter.ProfessionalId is { } professionalId)
        {
            appointments = appointments.Where(appointment => appointment.ProfessionalId == professionalId);
        }

        if (filter.CustomerId is { } customerId)
        {
            appointments = appointments.Where(appointment => appointment.CustomerId == customerId);
        }

        if (filter.AllowedBranchIds is { } allowed)
        {
            var allowedIds = allowed.ToList();
            appointments = appointments.Where(appointment => allowedIds.Contains(appointment.BranchId));
        }

        if (filter.FromUtc is { } fromUtc)
        {
            appointments = appointments.Where(appointment => appointment.Period.Start >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            appointments = appointments.Where(appointment => appointment.Period.Start < toUtc);
        }

        if (filter.Status is { } status)
        {
            appointments = appointments.Where(appointment => appointment.Status == status);
        }

        if (filter.UpcomingFromUtc is { } upcomingFromUtc)
        {
            // Pending, confirmed and in progress: what still takes up the agenda (Appointment.OccupiesSchedule).
            AppointmentStatus[] occupying =
                [AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InProgress];

            appointments = appointments.Where(appointment =>
                appointment.Period.Start >= upcomingFromUtc && occupying.Contains(appointment.Status));
        }

        return appointments;
    }
}