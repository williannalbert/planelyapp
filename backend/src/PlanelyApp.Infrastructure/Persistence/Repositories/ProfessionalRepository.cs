using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class ProfessionalRepository(PlanelyDbContext context)
    : Repository<Professional>(context), IProfessionalRepository;