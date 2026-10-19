namespace PlanelyApp.Domain.Catalog.Promotions;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Promotion promotion);
}