using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Imports.List;

internal sealed class ListImportsValidator : AbstractValidator<ListImportsQuery>
{
    public ListImportsValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}