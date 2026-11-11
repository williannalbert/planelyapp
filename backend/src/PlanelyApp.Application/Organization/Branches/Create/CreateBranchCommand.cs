using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Branches.Create;

/// <summary>Creates a branch in the company of the request.</summary>
public sealed record CreateBranchCommand(string Name, AddressData Address, string Phone, string TimeZoneId)
    : ICommand<Guid>;