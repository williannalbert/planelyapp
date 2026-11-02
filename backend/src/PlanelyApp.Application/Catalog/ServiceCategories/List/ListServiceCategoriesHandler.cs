using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceCategories.List;

internal sealed class ListServiceCategoriesHandler(IServiceCategoryReadModel readModel)
    : IQueryHandler<ListServiceCategoriesQuery, IReadOnlyList<ServiceCategoryDto>>
{
    public async Task<Result<IReadOnlyList<ServiceCategoryDto>>> HandleAsync(
        ListServiceCategoriesQuery query,
        CancellationToken cancellationToken) =>
        Result<IReadOnlyList<ServiceCategoryDto>>.Success(await readModel.ListAsync(cancellationToken));
}