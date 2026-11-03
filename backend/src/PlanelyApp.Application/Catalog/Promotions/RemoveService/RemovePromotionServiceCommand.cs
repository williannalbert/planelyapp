using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.RemoveService;

/// <summary>
/// Takes a service out of a promotion. One that was not in it is ignored; the last one cannot be removed,
/// and a bundle cannot go below two.
/// </summary>
public sealed record RemovePromotionServiceCommand(Guid PromotionId, Guid ServiceOfferingId) : ICommand<Unit>;