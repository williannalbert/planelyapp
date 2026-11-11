using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Availability.GetFreeSlots;

internal sealed class GetFreeSlotsValidator : AbstractValidator<GetFreeSlotsQuery>
{
    // An appointment never has this many services; the limit keeps the query small.
    private const int MaxServices = 50;

    public GetFreeSlotsValidator()
    {
        RuleFor(query => query.BranchId).NotEmpty();
        RuleFor(query => query.Date).NotEmpty();
        RuleFor(query => query.ServiceIds).NotEmpty();
        RuleFor(query => query.ServiceIds)
            .Must(ids => ids.Count <= MaxServices)
            .When(query => query.ServiceIds is not null);
        RuleForEach(query => query.ServiceIds).NotEqual(Guid.Empty);
        RuleFor(query => query.ProfessionalId).NotEqual(Guid.Empty);

        // A step of a whole number of minutes, up to a day. The Domain checks it again.
        RuleFor(query => query.StepMinutes).InclusiveBetween(1, 1440);
    }
}