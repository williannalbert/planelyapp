using FluentValidation;
using PlanelyApp.Application.Abstractions.Paging;

namespace PlanelyApp.Application.Scheduling.Appointments.ListByProfessional;

internal sealed class ListProfessionalAppointmentsValidator : AbstractValidator<ListProfessionalAppointmentsQuery>
{
    private const int MaxDays = 31;

    public ListProfessionalAppointmentsValidator()
    {
        RuleFor(query => query.ProfessionalId).NotEmpty();

        RuleFor(query => query.FromUtc).NotEmpty();
        RuleFor(query => query.FromUtc).Must(IsUtc).When(query => query.FromUtc != default);
        RuleFor(query => query.ToUtc).NotEmpty();
        RuleFor(query => query.ToUtc).Must(IsUtc).When(query => query.ToUtc != default);

        RuleFor(query => query.ToUtc)
            .GreaterThan(query => query.FromUtc)
            .When(query => query.FromUtc != default && query.ToUtc != default);
        RuleFor(query => query.ToUtc)
            .Must((query, toUtc) => toUtc - query.FromUtc <= TimeSpan.FromDays(MaxDays))
            .When(query => query.FromUtc != default && query.ToUtc > query.FromUtc);

        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Page).InclusiveBetween(1, PageLimits.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageLimits.MaxPageSize);
    }

    // A time that is not UTC would be read as another instant by the data store.
    private static bool IsUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}