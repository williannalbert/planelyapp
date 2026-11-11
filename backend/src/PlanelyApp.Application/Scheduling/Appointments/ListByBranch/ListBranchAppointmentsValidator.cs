using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByBranch;

internal sealed class ListBranchAppointmentsValidator : AbstractValidator<ListBranchAppointmentsQuery>
{
    private const int MaxDays = 31;

    // Wide enough for any real date, narrow enough that "the day after" never overflows.
    private static readonly DateOnly Earliest = new(2000, 1, 1);
    private static readonly DateOnly Latest = new(2200, 1, 1);

    public ListBranchAppointmentsValidator()
    {
        RuleFor(query => query.BranchId).NotEmpty();
        RuleFor(query => query.ProfessionalId).NotEqual(Guid.Empty);
        RuleFor(query => query.FromDate).InclusiveBetween(Earliest, Latest);
        RuleFor(query => query.ToDate).InclusiveBetween(Earliest, Latest);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .When(query => IsValid(query.FromDate) && IsValid(query.ToDate));
        RuleFor(query => query.ToDate)
            .Must((query, toDate) => toDate.DayNumber - query.FromDate.DayNumber < MaxDays)
            .When(query => IsValid(query.FromDate) && IsValid(query.ToDate) && query.ToDate >= query.FromDate);
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }

    private static bool IsValid(DateOnly date) => date >= Earliest && date <= Latest;
}