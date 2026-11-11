using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByCustomer;

internal sealed class ListCustomerAppointmentsHandler(
    ICustomerRepository customers,
    BranchScopeResolver scopes,
    ICurrentUser currentUser,
    IAppointmentReadModel readModel,
    TimeProvider timeProvider)
    : IQueryHandler<ListCustomerAppointmentsQuery, PagedResult<AppointmentSummaryDto>>
{
    public async Task<Result<PagedResult<AppointmentSummaryDto>>> HandleAsync(
        ListCustomerAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        // A customer sees all their appointments, in every branch. The staff, only the branches they may see.
        IReadOnlyCollection<Guid>? allowedBranchIds = null;

        if (currentUser.Kind == ActorKind.Customer)
        {
            // Another customer does not exist for this one: a 403 would say that it does.
            if (query.CustomerId != currentUser.Id)
            {
                return Error.NotFound("Customer.NotFound");
            }
        }
        else
        {
            var scope = await scopes.ResolveForStaffAsync(cancellationToken);

            if (scope.IsFailure)
            {
                return scope.Error;
            }

            if (await customers.GetByIdAsync(query.CustomerId, cancellationToken) is null)
            {
                return Error.NotFound("Customer.NotFound");
            }

            allowedBranchIds = scope.Value.BranchIds;
        }

        DateTime? upcomingFromUtc = query.OnlyUpcoming ? timeProvider.GetUtcNow().UtcDateTime : null;

        var filter = new AppointmentFilter(
            CustomerId: query.CustomerId,
            Status: query.Status,
            AllowedBranchIds: allowedBranchIds,
            UpcomingFromUtc: upcomingFromUtc,
            NewestFirst: !query.OnlyUpcoming);

        return await readModel.ListAsync(filter, query.Page, query.PageSize, cancellationToken);
    }
}