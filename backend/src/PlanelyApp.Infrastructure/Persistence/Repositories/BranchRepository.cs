using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class BranchRepository(PlanelyDbContext context)
    : Repository<Branch>(context), IBranchRepository;