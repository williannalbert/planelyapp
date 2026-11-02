using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.ServiceCategories.List;

/// <summary>The categories of the company of the request, ordered by name.</summary>
public sealed record ListServiceCategoriesQuery : IQuery<IReadOnlyList<ServiceCategoryDto>>;