namespace PlanelyApp.Domain.SeedWork;

/// <summary>
/// The save failed because someone else changed the same data first (a concurrency token, a deadlock or a lock wait
/// that ran out). Nothing was saved. It is not a rule of the business, so it is not a DomainException: the caller may
/// send the request again.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data changed while the request was running: nothing was saved.", innerException);