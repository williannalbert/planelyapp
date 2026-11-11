namespace PlanelyApp.Application.Imports.Processing;

/// <summary>An import to work on, and the company the job must act as to work on it.</summary>
public sealed record PendingImport(Guid ImportId, Guid CompanyId);

/// <summary>
/// The read that looks at every company at once, for the job (like <c>IDueReminderReadModel</c>). It only reads and only
/// returns ids: each import is then handled inside the scope of its own company.
/// </summary>
public interface IPendingImportReadModel
{
    /// <summary>Imports that are pending or still processing, the oldest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<PendingImport>> ListAsync(int limit, CancellationToken cancellationToken);
}