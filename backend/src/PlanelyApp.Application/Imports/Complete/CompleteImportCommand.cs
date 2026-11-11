using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Imports.Complete;

/// <summary>Sent by the job when every row was counted. The import ends as completed, or completed with errors.</summary>
public sealed record CompleteImportCommand(Guid ImportId) : ICommand<Unit>;