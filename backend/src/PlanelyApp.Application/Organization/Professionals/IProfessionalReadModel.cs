namespace PlanelyApp.Application.Organization.Professionals;

/// <summary>Reads professionals for lists, straight from the data store. The company of the request applies.</summary>
public interface IProfessionalReadModel
{
    /// <summary>The professionals of the company ordered by name. With a value in <paramref name="isActive"/>, only those.</summary>
    Task<IReadOnlyList<ProfessionalSummaryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken);
}