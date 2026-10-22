using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Identity.AccessCodes;

public interface IAccessCodeRepository : IRepository<AccessCode>
{
    // The destination is the normalized value (Email.Value or Phone.Value).

    /// <summary>The most recent code issued to the destination, used or not: the one the customer is typing.</summary>
    Task<AccessCode?> GetLatestAsync(Guid companyId, string destination, CancellationToken cancellationToken = default);

    /// <summary>When the codes issued to the destination since the given instant were created (RN22).</summary>
    Task<IReadOnlyList<DateTime>> GetCreationTimesSinceAsync(
        Guid companyId,
        string destination,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}