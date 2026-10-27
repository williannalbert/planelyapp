using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

// Queries such as "promotions in force" arrive with the use case that needs them, in phase 4.
internal sealed class PromotionRepository(PlanelyDbContext context)
    : Repository<Promotion>(context), IPromotionRepository;