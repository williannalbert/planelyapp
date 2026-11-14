using Microsoft.Extensions.Logging;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Pipeline;

public class RetryOnConflictTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_command_that_lost_a_race_should_run_again_from_the_start_and_succeed()
    {
        await using var context = new PipelineContext();
        context.UnitOfWork.FailNextSaves = 1;

        var result = await context.Dispatcher.SendAsync(new BookSlotCommand(), _ct);

        result.IsSuccess.ShouldBeTrue();
        context.UnitOfWork.Saves.ShouldBe(1);
        context.UnitOfWork.Discards.ShouldBe(1);
        context.Operations.Entries.ShouldBe(
            ["book", "save-lost-race", "discard", "book", "save"], ignoreOrder: false);
    }

    [Fact]
    public async Task A_command_should_run_at_most_three_times_and_then_give_the_conflict_up()
    {
        await using var context = new PipelineContext();
        context.UnitOfWork.FailNextSaves = 3;

        await Should.ThrowAsync<ConcurrencyConflictException>(
            () => context.Dispatcher.SendAsync(new BookSlotCommand(), _ct));

        context.UnitOfWork.Saves.ShouldBe(0);
        context.UnitOfWork.Discards.ShouldBe(2);
        context.Operations.Entries.Count(entry => entry == "book").ShouldBe(3);
    }

    [Fact]
    public async Task A_command_that_did_not_ask_for_it_should_not_be_run_again()
    {
        await using var context = new PipelineContext();
        context.UnitOfWork.FailNextSaves = 1;

        await Should.ThrowAsync<ConcurrencyConflictException>(
            () => context.Dispatcher.SendAsync(new PlaceOrderCommand("coffee", 2), _ct));

        context.UnitOfWork.Discards.ShouldBe(0);
        context.Operations.Entries.Count(entry => entry == "handler").ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_result_should_not_be_run_again()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(new BookSlotCommand("taken"), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("Slot.Taken");
        context.UnitOfWork.Discards.ShouldBe(0);
        context.Operations.Entries.ShouldBe(["book"], ignoreOrder: false);
    }

    [Fact]
    public async Task Any_other_exception_should_not_be_run_again()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => context.Dispatcher.SendAsync(new BookSlotCommand("boom"), _ct));

        context.UnitOfWork.Discards.ShouldBe(0);
        context.Operations.Entries.ShouldBe(["book"], ignoreOrder: false);
    }

    [Fact]
    public async Task Running_again_should_be_logged_with_the_name_of_the_request()
    {
        await using var context = new PipelineContext();
        context.UnitOfWork.FailNextSaves = 1;

        await context.Dispatcher.SendAsync(new BookSlotCommand(), _ct);

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("BookSlotCommand")
            && entry.Message.Contains("lost a race"));
    }
}
