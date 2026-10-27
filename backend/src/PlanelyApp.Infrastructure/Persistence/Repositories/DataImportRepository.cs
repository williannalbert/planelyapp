using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

// Listing the imports and finding the pending ones arrive with the use cases that need them, in phase 4.
internal sealed class DataImportRepository(PlanelyDbContext context)
    : Repository<DataImport>(context), IDataImportRepository;