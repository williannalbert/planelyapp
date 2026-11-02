using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Catalog.BranchOfferings.List;

internal sealed class ListBranchOfferingsValidator : AbstractValidator<ListBranchOfferingsQuery>
{
    public ListBranchOfferingsValidator()
    {
        RuleFor(query => query.BranchId).NotEmpty();
        RuleFor(query => query.CategoryId).NotEqual(Guid.Empty);
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}