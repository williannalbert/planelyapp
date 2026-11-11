using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users;

/// <summary>Reads users for lists. Users have no tenant filter, so the company is always a parameter.</summary>
public interface IUserReadModel
{
    /// <summary>The users of the company ordered by name. With a role or a status, only those.</summary>
    Task<IReadOnlyList<UserSummaryDto>> ListAsync(
        Guid companyId,
        UserRole? role,
        bool? isActive,
        CancellationToken cancellationToken);
}