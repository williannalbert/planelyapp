using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Branches.Get;

public sealed record GetBranchQuery(Guid BranchId) : IQuery<BranchDto>;