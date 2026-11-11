using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Get;

internal sealed class GetImportHandler(IDataImportRepository imports)
    : IQueryHandler<GetImportQuery, DataImportDto>
{
    public async Task<Result<DataImportDto>> HandleAsync(GetImportQuery query, CancellationToken cancellationToken)
    {
        var import = await imports.GetByIdAsync(query.ImportId, cancellationToken);

        if (import is null)
        {
            return Error.NotFound("DataImport.NotFound");
        }

        return new DataImportDto(
            import.Id,
            import.Type,
            import.FileName,
            import.Status,
            import.TotalRows,
            import.ProcessedRows,
            import.SuccessRows,
            import.ErrorRows,
            import.ErrorsAreTruncated,
            import.FailureReason,
            import.RequestedBy,
            import.CreatedAtUtc,
            import.CompletedAtUtc,
            import.Errors.Select(error => new ImportErrorDto(error.Row, error.Reason)).ToList());
    }
}