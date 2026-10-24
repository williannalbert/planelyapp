namespace PlanelyApp.Infrastructure.Persistence.Conventions;

/// <summary>
/// A DateTime that is not UTC reached the database layer. This is a bug in the caller, not a business rule,
/// so it is not a DomainException and has no translated message.
/// </summary>
public sealed class NonUtcDateTimeException(DateTime value)
    : InvalidOperationException($"Only DateTime values with Kind=Utc can be stored, but got Kind={value.Kind} ({value:O}).");