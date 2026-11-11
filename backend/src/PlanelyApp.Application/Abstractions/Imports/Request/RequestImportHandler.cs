using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Imports.Request;

internal sealed class RequestImportHandler(
    IDataImportRepository imports,
    IImportFileStore files,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<RequestImportCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RequestImportCommand command, CancellationToken cancellationToken)
    {
        // Costs nothing, so it comes first: a customer cannot even leave a file behind. The id of who asked is kept.
        if (currentUser.Kind != ActorKind.Staff)
        {
            return Error.Forbidden("DataImport.StaffOnly");
        }

        var companyId = tenantProvider.RequireCompanyId();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // The validator guarantees there is a content. If the Domain refuses something after this (a name too long),
        // the file stays without an import: a cleanup of those is a known pending item.
        var storagePath = await files.SaveAsync(companyId, command.Content!, cancellationToken);

        var import = DataImport.Request(
            companyId, command.Type, command.FileName?.Trim(), storagePath, currentUser.Id, nowUtc);
        imports.Add(import);

        return import.Id;
    }
}