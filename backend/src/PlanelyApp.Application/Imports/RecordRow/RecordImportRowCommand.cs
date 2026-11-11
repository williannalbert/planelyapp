using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Imports.RecordRow;

/// <summary>Sent by the job after a row was imported (<paramref name="FailureReason"/> null) or refused.</summary>
public sealed record RecordImportRowCommand(Guid ImportId, int Row, string? FailureReason) : ICommand<Unit>;