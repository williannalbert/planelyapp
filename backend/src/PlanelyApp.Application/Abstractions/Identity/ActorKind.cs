namespace PlanelyApp.Application.Abstractions.Identity;

public enum ActorKind
{
    /// <summary>A user of the company (or of the platform), logged in through Keycloak.</summary>
    Staff,

    /// <summary>A customer, logged in with an access code.</summary>
    Customer,
}