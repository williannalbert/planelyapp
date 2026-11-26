namespace PlanelyApp.Api.Endpoints.Organization.Users;

/// <summary>
/// The body of <c>PUT /users/{userId}/branch-access</c>. <c>AccessAllBranches</c> must be said: left out, it would be false with an
/// empty list, and the user would lose every branch.
/// </summary>
internal sealed record BranchAccessBody(bool? AccessAllBranches, IReadOnlyList<Guid>? BranchIds = null);