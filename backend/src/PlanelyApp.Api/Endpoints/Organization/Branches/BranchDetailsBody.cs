using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Api.Endpoints.Organization.Branches;

/// <summary>The body of <c>PUT /branches/{branchId}</c>. No id: the branch is the one of the route.</summary>
internal sealed record BranchDetailsBody(string Name, AddressData Address, string Phone, string TimeZoneId);