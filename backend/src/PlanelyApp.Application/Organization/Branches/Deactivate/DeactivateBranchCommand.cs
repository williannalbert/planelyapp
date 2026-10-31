using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Branches.Deactivate;

public sealed record DeactivateBranchCommand(Guid BranchId) : ICommand<Unit>;