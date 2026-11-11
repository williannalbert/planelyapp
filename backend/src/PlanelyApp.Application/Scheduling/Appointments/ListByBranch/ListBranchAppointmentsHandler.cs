using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByBranch;

internal sealed class ListBranchAppointmentsHandler(
    IBranchRepository branches,
    BranchScopeResolver scopes,
    IAppointmentReadModel readModel)
    : IQueryHandler<ListBranchAppointmentsQuery, PagedResult<AppointmentSummaryDto>>
{
    public async Task<Result<PagedResult<AppointmentSummaryDto>>> HandleAsync(
        ListBranchAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveForStaffAsync(cancellationToken);

        if (scope.IsFailure)
        {
            return scope.Error;
        }

        var branch = await branches.GetByIdAsync(query.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        if (!scope.Value.Allows(branch.Id))
        {
            return Error.Forbidden("Branch.AccessDenied");
        }

        // The days are days at the branch; the stored times are UTC. The end is the start of the day after the last.
        var fromUtc = branch.ToUtc(query.FromDate, TimeOnly.MinValue);
        var toUtc = branch.ToUtc(query.ToDate.AddDays(1), TimeOnly.MinValue);

        var filter = new AppointmentFilter(
            BranchId: branch.Id,
            ProfessionalId: query.ProfessionalId,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Status: query.Status);

        return await readModel.ListAsync(filter, query.Page, query.PageSize, cancellationToken);
    }
}