using PlanelyApp.Domain.Catalog.ServiceCategories;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class ServiceCategoryRepository(PlanelyDbContext context)
    : Repository<ServiceCategory>(context), IServiceCategoryRepository;