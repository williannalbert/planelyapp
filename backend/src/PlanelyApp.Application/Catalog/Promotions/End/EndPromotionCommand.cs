using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.End;

/// <summary>Ends a promotion: nothing is deleted and appointments already made keep their price. Ending twice is fine.</summary>
public sealed record EndPromotionCommand(Guid PromotionId) : ICommand<Unit>;