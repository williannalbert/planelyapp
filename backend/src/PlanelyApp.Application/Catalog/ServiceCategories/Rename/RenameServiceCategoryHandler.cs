using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.ServiceCategories;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Rename;

internal sealed class RenameServiceCategoryHandler(IServiceCategoryRepository categories)
    : ICommandHandler<RenameServiceCategoryCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RenameServiceCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(command.ServiceCategoryId, cancellationToken);

        if (category is null)
        {
            return Error.NotFound("ServiceCategory.NotFound");
        }

        var name = command.Name.Trim();

        // The category itself is left out, so keeping its name (or only changing the case) is not a clash.
        if (await categories.ExistsByNameAsync(name, category.Id, cancellationToken))
        {
            return Error.Conflict("ServiceCategory.NameAlreadyExists");
        }

        category.Rename(name);

        return Result.Success();
    }
}