using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Catalog.ServiceCategories.Create;

internal sealed class CreateServiceCategoryHandler(IServiceCategoryRepository categories, ITenantProvider tenantProvider)
    : ICommandHandler<CreateServiceCategoryCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CreateServiceCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var name = command.Name.Trim();

        if (await categories.ExistsByNameAsync(name, null, cancellationToken))
        {
            return Error.Conflict("ServiceCategory.NameAlreadyExists");
        }

        var category = ServiceCategory.Create(companyId, name);

        categories.Add(category);

        return category.Id;
    }
}