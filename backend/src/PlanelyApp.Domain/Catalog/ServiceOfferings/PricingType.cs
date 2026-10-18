namespace PlanelyApp.Domain.Catalog.ServiceOfferings;

public enum PricingType
{
    /// <summary>The service has a base price.</summary>
    Fixed,

    /// <summary>The service has no price up front; the professional sets it when confirming the appointment.</summary>
    OnRequest,
}