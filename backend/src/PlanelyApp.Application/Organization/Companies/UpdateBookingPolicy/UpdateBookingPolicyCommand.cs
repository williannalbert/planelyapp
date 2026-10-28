using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;

/// <summary>Replaces the whole booking policy of the company of the request.</summary>
public sealed record UpdateBookingPolicyCommand(BookingPolicyData Policy) : ICommand<Unit>;