using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByCustomer;

internal sealed class ListCustomerAppointmentsValidator : AbstractValidator<ListCustomerAppointmentsQuery>
{
    public ListCustomerAppointmentsValidator()
    {
        RuleFor(query => query.CustomerId).NotEmpty();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }
}