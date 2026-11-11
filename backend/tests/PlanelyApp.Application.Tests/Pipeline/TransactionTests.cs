using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Pipeline;

public class TransactionTests
{
    [Fact]
    public async Task A_successful_command_should_save_once_after_its_events()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("coffee", 2), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        context.UnitOfWork.Saves.ShouldBe(1);
        context.Operations.Entries.ShouldBe(
            ["handler", "event:OrderPlaced", "event:OrderNotified", "save"], ignoreOrder: false);
    }

    [Fact]
    public async Task A_failed_result_should_save_nothing_and_dispatch_nothing()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("missing", 1), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        context.UnitOfWork.Saves.ShouldBe(0);
        context.Operations.Entries.ShouldBe(["handler"], ignoreOrder: false);
    }

    [Fact]
    public async Task An_exception_should_save_nothing()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<InvalidOperationException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("boom", 1), TestContext.Current.CancellationToken));

        context.UnitOfWork.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task A_rule_refused_by_the_domain_should_save_nothing()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<DomainException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("refused", 1), TestContext.Current.CancellationToken));

        context.UnitOfWork.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task A_validation_failure_should_save_nothing_and_not_even_reach_the_handler()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.SendAsync(new PlaceOrderCommand("", 0), TestContext.Current.CancellationToken);

        context.UnitOfWork.Saves.ShouldBe(0);
        context.Operations.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_event_without_a_handler_should_be_ignored()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("quiet", 1), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        context.Operations.Entries.ShouldBe(["handler", "save"], ignoreOrder: false);
    }

    [Fact]
    public async Task Events_that_never_settle_should_be_stopped_and_nothing_saved()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<InvalidOperationException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("loop", 1), TestContext.Current.CancellationToken));

        context.UnitOfWork.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task A_failing_event_handler_should_prevent_the_save()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<NotSupportedException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("faulty", 1), TestContext.Current.CancellationToken));

        context.UnitOfWork.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task A_query_should_never_save()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.QueryAsync(new ListOrdersQuery("open"), TestContext.Current.CancellationToken);

        context.UnitOfWork.Saves.ShouldBe(0);
        context.Operations.Entries.ShouldBe(["query-handler"], ignoreOrder: false);
    }
}