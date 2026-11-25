using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.Api;

/// <summary>
/// Stands in for the pieces that belong to later subphases, so the container can be built today. Each one throws
/// when used, saying which subphase brings the real one; that subphase deletes its line here.
/// </summary>
internal static class PendingPieces
{
    public static IServiceCollection AddPendingPieces(this IServiceCollection services) =>
        services
            .AddSingleton<IAccessCodeSender, PendingAccessCodeSender>()  // 5.8: e-mail or SMS
            .AddSingleton<IImportFileStore, PendingImportFileStore>();   // 5.9: the folder of the spreadsheets

    internal static NotSupportedException NotYet(string piece, string phase) =>
        new($"{piece} has no implementation yet: it comes in {phase}.");
}

internal sealed class PendingAccessCodeSender : IAccessCodeSender
{
    public Task SendAsync(AccessCodeDelivery delivery, CancellationToken cancellationToken) =>
        throw PendingPieces.NotYet(nameof(IAccessCodeSender), "5.8");
}

internal sealed class PendingImportFileStore : IImportFileStore
{
    public Task<string> SaveAsync(Guid companyId, Stream content, CancellationToken cancellationToken) =>
        throw PendingPieces.NotYet(nameof(IImportFileStore), "5.9");

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        throw PendingPieces.NotYet(nameof(IImportFileStore), "5.9");
}