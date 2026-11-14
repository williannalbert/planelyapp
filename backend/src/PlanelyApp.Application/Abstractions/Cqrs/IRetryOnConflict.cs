namespace PlanelyApp.Application.Abstractions.Cqrs;

/// <summary>
/// Marks a command that may run again, from the start, after losing a race (<c>ConcurrencyConflictException</c>).
/// Only for a command with no effect outside the database: a second run does everything again, so a call to Keycloak,
/// an e-mail or a file written along the way would happen twice.
/// </summary>
public interface IRetryOnConflict;