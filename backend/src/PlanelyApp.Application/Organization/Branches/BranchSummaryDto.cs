namespace PlanelyApp.Application.Organization.Branches;

public sealed record BranchSummaryDto(Guid Id, string Name, string City, string State, string Phone, bool IsActive);