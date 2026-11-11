using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Branches.List;

/// <summary>The branches of the company of the request. With <see cref="IsActive"/>, only the active or the inactive ones.</summary>
public sealed record ListBranchesQuery(bool? IsActive = null) : IQuery<IReadOnlyList<BranchSummaryDto>>;