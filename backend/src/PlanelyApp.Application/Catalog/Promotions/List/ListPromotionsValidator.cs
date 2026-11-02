using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.Promotions.List;

internal sealed class ListPromotionsValidator : AbstractValidator<ListPromotionsQuery>
{
    public ListPromotionsValidator()
    {
        RuleFor(query => query.Kind).IsInEnum();
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}