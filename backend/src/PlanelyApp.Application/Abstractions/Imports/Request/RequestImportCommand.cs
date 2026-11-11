using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Request;

/// <summary>
/// Asks for the import of a spreadsheet of services or customers into the company of the request. The file is saved
/// and the import is created as pending; a background job processes it (4.14b). Only the staff can ask.
/// <paramref name="Content"/> must be a stream that can be read and repositioned, so its size is known before anything is saved.
/// The caller owns the stream and disposes it.
/// </summary>
public sealed record RequestImportCommand(ImportType Type, string? FileName, Stream? Content) : ICommand<Guid>;