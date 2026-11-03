using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.Promotions;

public interface IPromotionRepository : IRepository<Promotion>
{
    /// <summary>
    /// The active promotions of the company of the request whose validity period contains this day (both ends
    /// inclusive). The day is the local date of the branch. Weekdays, branches and services are the promotion's to
    /// judge (<see cref="Promotion.AppliesTo"/>), so they are not filtered here.
    /// </summary>
    Task<IReadOnlyList<Promotion>> ListInForceAsync(DateOnly localDate, CancellationToken cancellationToken = default);
}