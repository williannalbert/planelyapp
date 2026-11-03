namespace PlanelyApp.Application.Catalog.Pricing;

/// <summary>The lines in the order asked and the totals. Services on request are left out of the totals.</summary>
public sealed record PriceQuoteDto(
    IReadOnlyList<PriceLineDto> Lines,
    decimal ListTotal,
    decimal DiscountTotal,
    decimal FinalTotal,
    bool HasServiceOnRequest);