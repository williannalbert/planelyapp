using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Complete;

internal sealed class CompleteImportHandler(IDataImportRepository imports, TimeProvider timeProvider)
    : ICommandHandler<CompleteImportCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(CompleteImportCommand command, CancellationToken cancellationToken)
    {
        var import = await imports.GetByIdAsync(command.ImportId, cancellationToken);

        if (import is null)
        {
            return Error.NotFound("DataImport.NotFound");
        }

        import.Complete(timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}