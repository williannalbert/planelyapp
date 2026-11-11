using System.Collections.Concurrent;
using PlanelyApp.Application.Abstractions.Identity;

namespace PlanelyApp.IntegrationTests;

/// <summary>Keeps the codes in memory instead of sending them, and can fail on demand for one destination.</summary>
public sealed class FakeAccessCodeSender : IAccessCodeSender
{
    private readonly ConcurrentDictionary<(Guid CompanyId, string Destination), string> _lastCodes = new();
    private readonly ConcurrentDictionary<(Guid CompanyId, string Destination), int> _sent = new();
    private readonly ConcurrentDictionary<string, byte> _failing = new();

    public Task SendAsync(AccessCodeDelivery delivery, CancellationToken cancellationToken)
    {
        if (_failing.ContainsKey(delivery.Destination))
        {
            throw new AccessCodeDeliveryException("The sender is down (test).");
        }

        var key = (delivery.CompanyId, delivery.Destination);
        _lastCodes[key] = delivery.Code;
        _sent.AddOrUpdate(key, 1, (_, count) => count + 1);

        return Task.CompletedTask;
    }

    public string CodeFor(Guid companyId, string destination) => _lastCodes[(companyId, destination)];

    public int SentTo(Guid companyId, string destination) => _sent.GetValueOrDefault((companyId, destination));

    public void FailFor(string destination) => _failing[destination] = 0;

    public void Recover(string destination) => _failing.TryRemove(destination, out _);
}