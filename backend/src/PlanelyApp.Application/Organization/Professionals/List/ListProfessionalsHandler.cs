using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.List;

internal sealed class ListProfessionalsHandler(IProfessionalReadModel readModel)
    : IQueryHandler<ListProfessionalsQuery, IReadOnlyList<ProfessionalSummaryDto>>
{
    public async Task<Result<IReadOnlyList<ProfessionalSummaryDto>>> HandleAsync(
        ListProfessionalsQuery query,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<ProfessionalSummaryDto>>.Success(
            await readModel.ListAsync(query.IsActive, cancellationToken));
}