using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.SetWeekdays;

/// <summary>Replaces the weekdays a promotion is in force. An empty list means every day.</summary>
public sealed record SetPromotionWeekdaysCommand(Guid PromotionId, IReadOnlyList<DayOfWeek> Weekdays) : ICommand<Unit>;