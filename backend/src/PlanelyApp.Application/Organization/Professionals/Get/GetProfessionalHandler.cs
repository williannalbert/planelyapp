using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals.Get;

internal sealed class GetProfessionalHandler(IProfessionalRepository professionals)
    : IQueryHandler<GetProfessionalQuery, ProfessionalDto>
{
    public async Task<Result<ProfessionalDto>> HandleAsync(
        GetProfessionalQuery query,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(query.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // The order of a collection read from the database is not guaranteed, so each one is ordered here.
        return new ProfessionalDto(
            professional.Id,
            professional.Name,
            professional.Specialty,
            professional.UserId,
            professional.IsActive,
            professional.WorkingHours
                .OrderBy(hours => hours.Schedule.Day)
                .ThenBy(hours => hours.Schedule.Range.Start)
                .ThenBy(hours => hours.BranchId)
                .Select(WorkingHoursData.From)
                .ToList(),
            professional.TimeOffs
                .OrderBy(timeOff => timeOff.Range.Start)
                .ThenBy(timeOff => timeOff.Range.End)
                .ThenBy(timeOff => timeOff.Id)
                .Select(TimeOffData.From)
                .ToList(),
            professional.Offerings
                .OrderBy(offering => offering.ServiceOfferingId)
                .Select(OfferingData.From)
                .ToList());
    }
}