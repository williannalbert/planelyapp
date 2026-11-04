using NSubstitute;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Catalog.Promotions.Events;
using PlanelyApp.Domain.Catalog.Promotions;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class PromotionOutboxHandlerTests
{
    private static readonly Guid PromotionId = Guid.CreateVersion7();
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly DateTime Occurred = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_promotion_for_some_branches_should_carry_them()
    {
        Guid[] branches = [Guid.CreateVersion7(), Guid.CreateVersion7()];

        var (outbox, sent) = await HandleAsync(new PromotionCreated(PromotionId, CompanyId, false, branches));

        outbox.Received(1).Add("promotion.created", CompanyId, Arg.Any<object>(), Occurred, 1);
        var message = sent().ShouldBeOfType<PromotionCreatedMessage>();
        message.PromotionId.ShouldBe(PromotionId);
        message.AppliesToAllBranches.ShouldBeFalse();
        message.BranchIds.ShouldBe(branches, ignoreOrder: false);
    }

    [Fact]
    public async Task A_promotion_for_all_branches_should_carry_an_empty_list()
    {
        var (outbox, sent) = await HandleAsync(new PromotionCreated(PromotionId, CompanyId, true, []));

        outbox.Received(1).Add("promotion.created", CompanyId, Arg.Any<object>(), Occurred, 1);
        var message = sent().ShouldBeOfType<PromotionCreatedMessage>();
        message.AppliesToAllBranches.ShouldBeTrue();
        message.BranchIds.ShouldBeEmpty();
    }

    private static async Task<(IOutbox Outbox, Func<object?> Sent)> HandleAsync(PromotionCreated domainEvent)
    {
        var outbox = Substitute.For<IOutbox>();
        object? sent = null;
        outbox.When(call => call.Add(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<object>(), Arg.Any<DateTime>(), Arg.Any<int>()))
            .Do(call => sent = call.ArgAt<object>(2));

        await new PromotionCreatedOutboxHandler(outbox)
            .HandleAsync(domainEvent with { OccurredAtUtc = Occurred }, TestContext.Current.CancellationToken);

        return (outbox, () => sent);
    }
}