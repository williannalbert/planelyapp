namespace PlanelyApp.Application.Abstractions.Messaging;

/// <summary>
/// Where a use case leaves the messages other systems must receive. The message joins the unit of work of the
/// request, so it is saved (or lost) together with the changes that caused it.
/// </summary>
public interface IOutbox
{
    /// <param name="type">What happened, as the consumers know it: "appointment.confirmed". Picks the exchange; the whole type is the routing key.</param>
    /// <param name="companyId">The company the fact belongs to.</param>
    /// <param name="data">The content of the message. It is turned into JSON right away, so later changes to the object do not leak in.</param>
    /// <param name="occurredAtUtc">When the fact happened (the instant of the domain event), not when it is published.</param>
    /// <param name="version">The version of the contract of <paramref name="data"/>. Consumers ignore fields they do not know.</param>
    void Add(string type, Guid companyId, object data, DateTime occurredAtUtc, int version = 1);
}