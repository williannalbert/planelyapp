using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.UpdateDetails;

/// <summary>Replaces the name and the description of a promotion. A <see cref="Description"/> left out clears it.</summary>
public sealed record UpdatePromotionDetailsCommand(
    Guid PromotionId,
    string Name,
    string? Description = null) : ICommand<Unit>;