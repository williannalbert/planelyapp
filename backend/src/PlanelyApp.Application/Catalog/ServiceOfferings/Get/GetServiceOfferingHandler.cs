using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Get;

internal sealed class GetServiceOfferingHandler(IServiceOfferingRepository services)
    : IQueryHandler<GetServiceOfferingQuery, ServiceOfferingDto>
{
    public async Task<Result<ServiceOfferingDto>> HandleAsync(
        GetServiceOfferingQuery query,
        CancellationToken cancellationToken)
    {
        var service = await services.GetByIdAsync(query.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        return new ServiceOfferingDto(
            service.Id,
            service.Name,
            service.Description,
            (int)service.StandardDuration.TotalMinutes,
            service.PricingType,
            service.BasePrice?.Amount,
            service.CategoryId,
            service.IsActive);
    }
}