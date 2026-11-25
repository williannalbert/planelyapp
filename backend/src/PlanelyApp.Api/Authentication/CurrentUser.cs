using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Authentication;

/// <summary>
/// Who is acting in this request, filled by <see cref="CurrentUserMiddleware"/> from the database (never from the claims).
/// It is the <see cref="ICurrentUser"/> of the use cases; <see cref="CompanyId"/> is for the Api itself (the tenant, 5.4).
/// </summary>
internal sealed class CurrentUser : ICurrentUser
{
    private StaffActor? _actor;

    public Guid Id => Require().UserId;

    public ActorKind Kind
    {
        get
        {
            Require();

            return ActorKind.Staff;
        }
    }

    public UserRole? Role => Require().Role;

    /// <summary>Null for the platform administrator.</summary>
    public Guid? CompanyId => Require().CompanyId;

    /// <summary>Whether the middleware has filled it: false for an endpoint that allows anonymous access.</summary>
    public bool IsResolved => _actor is not null;

    /// <summary>RN23: the company of the route is the company of the user. The platform administrator may enter any.</summary>
    public bool MayActInCompany(Guid companyId)
    {
        var actor = Require();

        return actor.Role == UserRole.PlatformAdmin || actor.CompanyId == companyId;
    }

    /// <summary>RN11.</summary>
    public bool CanAccessBranch(Guid branchId) => Require().CanAccessBranch(branchId);

    public void Use(StaffActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        _actor = actor;
    }

    private StaffActor Require() =>
        _actor ?? throw new InvalidOperationException("Nobody is authenticated in this request.");
}