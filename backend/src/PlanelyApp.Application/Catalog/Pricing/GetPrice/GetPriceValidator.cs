using FluentValidation;

namespace PlanelyApp.Application.Catalog.Pricing.GetPrice;

internal sealed class GetPriceValidator : AbstractValidator<GetPriceQuery>
{
    // An appointment never has this many services; the limit protects the search for the best bundles.
    private const int MaxServices = 50;

    public GetPriceValidator()
    {
        // That the time is UTC is the Domain's (Branch.TimeMustBeUtc).
        RuleFor(query => query.BranchId).NotEmpty();
        RuleFor(query => query.StartUtc).NotEmpty();
        RuleFor(query => query.ServiceIds).NotEmpty();
        RuleFor(query => query.ServiceIds)
            .Must(ids => ids.Count <= MaxServices)
            .When(query => query.ServiceIds is not null);
        RuleForEach(query => query.ServiceIds).NotEqual(Guid.Empty);
    }
}