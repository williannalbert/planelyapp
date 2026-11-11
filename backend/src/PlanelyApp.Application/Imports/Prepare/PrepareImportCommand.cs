using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Imports.Processing;

namespace PlanelyApp.Application.Imports.Prepare;

/// <summary>
/// Sent by the job, not by a person. Opens the file, checks it and moves the import to Processing, or fails it. The
/// response has no work when there is nothing left to do (the import failed now, or was already finished).
/// </summary>
public sealed record PrepareImportCommand(Guid ImportId) : ICommand<PrepareImportResponse>;

public sealed record PrepareImportResponse(ImportWork? Work);