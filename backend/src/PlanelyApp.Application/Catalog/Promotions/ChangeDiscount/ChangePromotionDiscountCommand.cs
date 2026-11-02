using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeDiscount;

/// <summary>Replaces the discount of a promotion. Appointments already made keep the price they got.</summary>
public sealed record ChangePromotionDiscountCommand(Guid PromotionId, DiscountData Discount) : ICommand<Unit>;