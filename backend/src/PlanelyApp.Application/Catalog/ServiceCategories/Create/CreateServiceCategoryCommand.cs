using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Create;

/// <summary>Creates a category in the company of the request. The name is unique inside the company.</summary>
public sealed record CreateServiceCategoryCommand(string Name) : ICommand<Guid>;