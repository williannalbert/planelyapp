using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions.Events;

internal sealed class PromotionCreatedOutboxHandler(IOutbox outbox) : IDomainEventHandler<PromotionCreated>
{
    public Task HandleAsync(PromotionCreated domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            PromotionMessageTypes.Created,
            domainEvent.CompanyId,
            new PromotionCreatedMessage(
                domainEvent.PromotionId,
                domainEvent.AppliesToAllBranches,
                [.. domainEvent.BranchIds]),
            domainEvent.OccurredAtUtc);

        return Task.CompletedTask;
    }
}