using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Organization.Users;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked.</summary>
internal sealed class UserReadModel(PlanelyDbContext context) : IUserReadModel
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListAsync(
        Guid companyId,
        UserRole? role,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        // Users have no tenant filter, so the company is part of every query.
        var users = context.Set<User>().AsNoTracking().Where(user => user.CompanyId == companyId);

        if (role is { } wantedRole)
        {
            users = users.Where(user => user.Role == wantedRole);
        }

        if (isActive is { } active)
        {
            users = users.Where(user => user.IsActive == active);
        }

        var rows = await users
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Id)
            .Select(user => new { user.Id, user.Name, user.Email, user.Role, user.IsActive, user.AccessAllBranches })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new UserSummaryDto(
                row.Id, row.Name, row.Email.Value, row.Role, row.IsActive, row.AccessAllBranches))
            .ToList();
    }
}