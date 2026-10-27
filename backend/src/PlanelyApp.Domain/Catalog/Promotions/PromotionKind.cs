namespace PlanelyApp.Domain.Catalog.Promotions;

public enum PromotionKind
{
    /// <summary>Discount on each listed service on its own.</summary>
    PerService = 0,

    /// <summary>Discount on the sum of all listed services, only when all of them are in the same appointment.</summary>
    Bundle = 1
}