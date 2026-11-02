using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Users.SetBranchAccess;

/// <summary>Replaces the whole branch access: every branch, or exactly <paramref name="BranchIds"/> (none, if empty).</summary>
public sealed record SetUserBranchAccessCommand(Guid UserId, bool AccessAllBranches, IReadOnlyList<Guid>? BranchIds = null)
    : ICommand<Unit>;