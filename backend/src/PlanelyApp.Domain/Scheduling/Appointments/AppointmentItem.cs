using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>
/// A service of the appointment with a copy (snapshot) of its name, duration and price at booking time,
/// so later changes to the service or the promotion do not change appointments already made.
/// </summary>
public sealed class AppointmentItem : Entity
{
    private AppointmentItem(
        Guid serviceOfferingId,
        string serviceName,
        TimeSpan duration,
        bool isOnRequest,
        Money? listPrice,
        Money discount,
        Money? finalPrice,
        Guid? promotionId)
    {
        ServiceOfferingId = serviceOfferingId;
        ServiceName = serviceName;
        Duration = duration;
        IsOnRequest = isOnRequest;
        ListPrice = listPrice;
        Discount = discount;
        FinalPrice = finalPrice;
        PromotionId = promotionId;
    }

    public Guid ServiceOfferingId { get; }

    public string ServiceName { get; }

    public TimeSpan Duration { get; private set; }

    /// <summary>True when the price is only agreed after booking (RN19). Stays true after the price is set.</summary>
    public bool IsOnRequest { get; }

    public Money? ListPrice { get; private set; }

    public Money Discount { get; }

    /// <summary>Null while the price of a service on request is still pending.</summary>
    public Money? FinalPrice { get; private set; }

    public Guid? PromotionId { get; }

    public bool HasPendingPrice => FinalPrice is null;

    // Only the Appointment aggregate creates its items.
    internal static AppointmentItem Create(NewAppointmentItem item)
    {
        var duration = Guard.DurationInWholeMinutes(item.Duration, "Appointment.ItemDurationInvalid");
        var serviceIsOnRequest = item.Service.PricingType == PricingType.OnRequest;

        // A quote calculated for a fixed price service cannot be used for a service on request, and vice versa.
        if (item.Price.IsOnRequest != serviceIsOnRequest)
        {
            throw new DomainException("Appointment.PriceDoesNotMatchService");
        }

        return new AppointmentItem(
            item.Service.Id,
            item.Service.Name,
            duration,
            item.Price.IsOnRequest,
            item.Price.ListPrice,
            item.Price.Discount,
            item.Price.FinalPrice,
            item.Price.PromotionId);
    }
}