using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.List;

internal sealed class ListServiceOfferingsValidator : AbstractValidator<ListServiceOfferingsQuery>
{
    public ListServiceOfferingsValidator()
    {
        RuleFor(query => query.CategoryId).NotEqual(Guid.Empty);
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}