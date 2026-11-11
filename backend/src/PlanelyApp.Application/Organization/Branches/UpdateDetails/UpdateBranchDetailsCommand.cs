using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Branches.UpdateDetails;

public sealed record UpdateBranchDetailsCommand(
    Guid BranchId,
    string Name,
    AddressData Address,
    string Phone,
    string TimeZoneId) : ICommand<Unit>;