using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Organization.Customers.List;

internal sealed class ListCustomersValidator : AbstractValidator<ListCustomersQuery>
{
    public ListCustomersValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}