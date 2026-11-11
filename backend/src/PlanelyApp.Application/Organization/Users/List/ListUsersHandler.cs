using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.List;

internal sealed class ListUsersHandler(IUserReadModel readModel, ITenantProvider tenantProvider)
    : IQueryHandler<ListUsersQuery, IReadOnlyList<UserSummaryDto>>
{
    public async Task<Result<IReadOnlyList<UserSummaryDto>>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<UserSummaryDto>>.Success(
            await readModel.ListAsync(tenantProvider.RequireCompanyId(), query.Role, query.IsActive, cancellationToken));
}