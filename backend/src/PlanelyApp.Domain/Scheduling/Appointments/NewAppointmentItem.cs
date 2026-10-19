using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Scheduling.Pricing;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>
/// What the caller already worked out for each service of a new appointment:
/// the duration (the professional may have a custom one) and the price quote (<see cref="PriceCalculator"/>).
/// </summary>
public sealed record NewAppointmentItem(ServiceOffering Service, TimeSpan Duration, PriceQuote Price);