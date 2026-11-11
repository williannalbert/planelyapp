using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByProfessional;

internal sealed class ListProfessionalAppointmentsHandler(
    IProfessionalRepository professionals,
    BranchScopeResolver scopes,
    IAppointmentReadModel readModel)
    : IQueryHandler<ListProfessionalAppointmentsQuery, PagedResult<AppointmentSummaryDto>>
{
    public async Task<Result<PagedResult<AppointmentSummaryDto>>> HandleAsync(
        ListProfessionalAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await scopes.ResolveForStaffAsync(cancellationToken);

        if (scope.IsFailure)
        {
            return scope.Error;
        }

        var professional = await professionals.GetByIdAsync(query.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The professional works in several branches: the staff only see the appointments of the ones they may (RN11).
        var filter = new AppointmentFilter(
            ProfessionalId: professional.Id,
            FromUtc: query.FromUtc,
            ToUtc: query.ToUtc,
            Status: query.Status,
            AllowedBranchIds: scope.Value.BranchIds);

        return await readModel.ListAsync(filter, query.Page, query.PageSize, cancellationToken);
    }
}