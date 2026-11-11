using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeValidity;

/// <summary>Replaces the period a promotion is in force. Both days are inclusive.</summary>
public sealed record ChangePromotionValidityCommand(
    Guid PromotionId,
    DateOnly ValidFrom,
    DateOnly ValidUntil) : ICommand<Unit>;