using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Rename;

public sealed record RenameServiceCategoryCommand(Guid ServiceCategoryId, string Name) : ICommand<Unit>;