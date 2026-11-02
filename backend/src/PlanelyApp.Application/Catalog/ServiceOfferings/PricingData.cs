using FluentValidation;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings;

/// <summary>How a service is priced: a fixed base price, or on request (no price up front, RN18).</summary>
public sealed record PricingData(PricingType Type, decimal? BasePrice = null);

internal sealed class PricingDataValidator : AbstractValidator<PricingData>
{
    public PricingDataValidator()
    {
        RuleFor(pricing => pricing.Type).IsInEnum();
        RuleFor(pricing => pricing.BasePrice).NotNull().When(pricing => pricing.Type == PricingType.Fixed);
        RuleFor(pricing => pricing.BasePrice).Null().When(pricing => pricing.Type == PricingType.OnRequest);

        // The column is decimal(12,2): more than two decimals would be rounded without notice, more than ten digits
        // before the point would not fit. Whether the price is not negative is the Domain's (Money.NegativeAmount).
        RuleFor(pricing => pricing.BasePrice)
            .PrecisionScale(12, 2, true)
            .When(pricing => pricing.BasePrice is not null);
    }
}