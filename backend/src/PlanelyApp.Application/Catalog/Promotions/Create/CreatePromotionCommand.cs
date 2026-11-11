using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.Create;

/// <summary>
/// Creates a promotion in the company of the request, for all branches or only for the ones listed.
/// A bundle discounts the sum of its services and needs at least two. Empty <see cref="Weekdays"/> means every day.
/// </summary>
public sealed record CreatePromotionCommand(
    PromotionKind Kind,
    string Name,
    DiscountData Discount,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    IReadOnlyList<Guid> ServiceIds,
    bool AppliesToAllBranches,
    IReadOnlyList<Guid>? BranchIds = null,
    IReadOnlyList<DayOfWeek>? Weekdays = null,
    string? Description = null) : ICommand<Guid>;