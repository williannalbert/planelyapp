using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Identity.AccessCodes;

namespace PlanelyApp.Application.Identity.AccessCodes.Request;

/// <summary>
/// Sends a new access code to the e-mail or phone, in the company of the request. It always succeeds for a valid
/// destination, whether or not there is a customer: the answer must not tell who is registered (RN24).
/// </summary>
public sealed record RequestAccessCodeCommand(AccessChannel Channel, string? Destination) : ICommand<Unit>;