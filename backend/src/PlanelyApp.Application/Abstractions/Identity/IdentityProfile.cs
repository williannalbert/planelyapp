using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>What the identity provider keeps about a person. A null language leaves the choice to the provider.</summary>
public sealed record IdentityProfile(string Name, Email Email, Language? Language);