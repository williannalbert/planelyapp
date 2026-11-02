using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.Promotions.Get;

public sealed record GetPromotionQuery(Guid PromotionId) : IQuery<PromotionDto>;