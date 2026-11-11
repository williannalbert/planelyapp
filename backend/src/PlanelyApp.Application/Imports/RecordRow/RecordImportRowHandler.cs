using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.RecordRow;

internal sealed class RecordImportRowHandler(IDataImportRepository imports)
    : ICommandHandler<RecordImportRowCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(RecordImportRowCommand command, CancellationToken cancellationToken)
    {
        var import = await imports.GetByIdAsync(command.ImportId, cancellationToken);

        if (import is null)
        {
            return Error.NotFound("DataImport.NotFound");
        }

        // If the import is not processing, the Domain refuses (DomainException): the job stops that import.
        if (command.FailureReason is null)
        {
            import.RecordSuccess();
        }
        else
        {
            import.RecordError(command.Row, command.FailureReason);
        }

        return Result.Success();
    }
}