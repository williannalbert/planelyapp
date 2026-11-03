using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.IncludeServices;

/// <summary>
/// Adds services to a promotion; the ones it already has are ignored. In a bundle, every service added becomes required.
/// </summary>
public sealed record IncludePromotionServicesCommand(Guid PromotionId, IReadOnlyList<Guid> ServiceIds) : ICommand<Unit>;