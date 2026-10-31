using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.EnableOffering;

internal sealed class EnableProfessionalOfferingHandler(
    IProfessionalRepository professionals,
    IServiceOfferingRepository services)
    : ICommandHandler<EnableProfessionalOfferingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        EnableProfessionalOfferingCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The Domain keeps only the id of the service. That it exists in this company is checked here, through the
        // repository, which already hides the services of other companies.
        var service = await services.GetByIdAsync(command.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        TimeSpan? customDuration = command.CustomDurationMinutes is { } minutes
            ? TimeSpan.FromMinutes(minutes)
            : null;

        professional.EnableOffering(service.Id, customDuration);

        return Result.Success();
    }
}