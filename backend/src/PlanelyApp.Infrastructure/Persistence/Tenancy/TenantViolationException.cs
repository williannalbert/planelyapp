namespace PlanelyApp.Infrastructure.Tenancy;

/// <summary>
/// Code tried to save data of a company that is not the one of the current scope. It is a bug or an attack,
/// never a business rule, so the API will log it and answer with an error, not a translated message.
/// </summary>
public sealed class TenantViolationException(string message) : InvalidOperationException(message);