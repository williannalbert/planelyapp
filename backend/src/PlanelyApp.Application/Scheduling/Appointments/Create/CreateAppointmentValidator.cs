using FluentValidation;

namespace PlanelyApp.Application.Scheduling.Appointments.Create;

internal sealed class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    // An appointment never has this many services; the limit keeps the request small.
    private const int MaxServices = 50;

    public CreateAppointmentValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.ProfessionalId).NotEmpty();
        RuleFor(command => command.StartUtc).NotEmpty();
        RuleFor(command => command.ServiceIds).NotEmpty();
        RuleFor(command => command.ServiceIds)
            .Must(ids => ids.Count <= MaxServices)
            .When(command => command.ServiceIds is not null);
        RuleForEach(command => command.ServiceIds).NotEqual(Guid.Empty);

        // The size of the notes and the time being UTC are the Domain's.
    }
}