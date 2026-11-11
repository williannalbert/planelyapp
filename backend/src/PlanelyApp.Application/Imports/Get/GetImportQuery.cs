using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Imports.Get;

/// <summary>An import of the company of the request, with its progress and the first errors.</summary>
public sealed record GetImportQuery(Guid ImportId) : IQuery<DataImportDto>;