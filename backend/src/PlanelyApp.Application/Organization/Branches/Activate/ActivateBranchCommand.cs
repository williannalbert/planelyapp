using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Branches.Activate;

public sealed record ActivateBranchCommand(Guid BranchId) : ICommand<Unit>;