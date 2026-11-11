using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Imports.Complete;
using PlanelyApp.Application.Imports.Prepare;
using PlanelyApp.Application.Imports.Processing;
using PlanelyApp.Application.Imports.RecordRow;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure.Imports;

/// <param name="Finished">Imports taken to an end in this pass (completed, or failed as a whole).</param>
/// <param name="Aborted">Imports interrupted by an unexpected problem. The next pass resumes them.</param>
public readonly record struct ImportRunResult(int Finished, int Aborted);

/// <summary>
/// One pass over the imports waiting: list them across every company, then for each one prepare it, import and count
/// its rows one by one, and complete it. Every step runs in its own scope with its company set, through the dispatcher
/// (validation, transaction, domain events and outbox as in a request). When it runs is the job of
/// <see cref="ImportWorkerService"/>; keeping it apart lets a test run exactly one pass.
/// </summary>
public sealed class ImportProcessor(
    IServiceScopeFactory scopeFactory,
    ImportOptions options,
    ILogger<ImportProcessor> logger)
{
    public async Task<ImportRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var pending = await ListPendingAsync(cancellationToken);
        int finished = 0, aborted = 0;

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessAsync(item, cancellationToken);
                finished++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // What was counted is saved, so the next pass resumes after it.
                aborted++;
                logger.LogError(exception, "Import {ImportId} was interrupted and will be resumed.", item.ImportId);
            }
        }

        if (pending.Count > 0)
        {
            logger.LogInformation("Imports: {Finished} finished, {Aborted} interrupted.", finished, aborted);
        }

        return new ImportRunResult(finished, aborted);
    }

    private async Task ProcessAsync(PendingImport item, CancellationToken cancellationToken)
    {
        var prepared = await SendAsync(item, new PrepareImportCommand(item.ImportId), cancellationToken);

        // Failed now, or already finished by someone else.
        if (prepared.Work is not { } work)
        {
            return;
        }

        for (var index = work.ProcessedRows; index < work.Rows.Count; index++)
        {
            var row = work.Rows[index];
            var outcome = await ImportRowAsync(item, work, row, cancellationToken);

            // If this fails, the row stays uncounted and the next pass imports it again.
            await SendAsync(item, new RecordImportRowCommand(item.ImportId, row.Number, outcome.Reason), cancellationToken);
        }

        await SendAsync(item, new CompleteImportCommand(item.ImportId), cancellationToken);
    }

    private async Task<ImportRowResult> ImportRowAsync(
        PendingImport item,
        ImportWork work,
        PlanelyApp.Application.Abstractions.Imports.ImportSheetRow row,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(item.CompanyId);

            return await scope.ServiceProvider.GetRequiredService<IImportRowImporter>()
                .ImportAsync(work, row, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not a rule of the Domain (the importer already answers those): something unexpected, logged with the details.
            logger.LogError(exception, "Row {Row} of import {ImportId} failed.", row.Number, item.ImportId);

            return ImportRowResult.Failed(ImportReasons.RowFailed);
        }
    }

    private async Task<TResponse> SendAsync<TResponse>(
        PendingImport item,
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(item.CompanyId);

        var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"{command.GetType().Name} of import {item.ImportId} was refused with {result.Error.Code}.");
    }

    private async Task<IReadOnlyList<PendingImport>> ListPendingAsync(CancellationToken cancellationToken)
    {
        // No company in this scope on purpose: the read model looks at all of them and only returns ids.
        await using var scope = scopeFactory.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IPendingImportReadModel>()
            .ListAsync(options.BatchSize, cancellationToken);
    }
}