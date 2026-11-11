using Microsoft.Extensions.Logging;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Pipeline;

public class LoggingTests
{
    [Fact]
    public async Task A_success_should_be_logged_with_the_name_of_the_request()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.SendAsync(new PlaceOrderCommand("coffee", 2), TestContext.Current.CancellationToken);

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("PlaceOrderCommand")
            && entry.Message.Contains("succeeded"));
    }

    [Fact]
    public async Task A_failed_result_should_be_logged_as_a_warning_with_its_code()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.SendAsync(new PlaceOrderCommand("missing", 1), TestContext.Current.CancellationToken);

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("failed")
            && entry.Message.Contains("NotFound")
            && entry.Message.Contains("Order.NotFound"));
    }

    [Fact]
    public async Task A_validation_failure_should_be_logged_too()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.SendAsync(new PlaceOrderCommand("", 0), TestContext.Current.CancellationToken);

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Validation.Failed"));
    }

    [Fact]
    public async Task A_rule_refused_by_the_domain_should_be_a_warning_and_still_reach_the_caller()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<DomainException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("refused", 1), TestContext.Current.CancellationToken));

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Order.Refused"));
        context.Logs.Entries.ShouldNotContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task An_unexpected_exception_should_be_logged_as_an_error_and_still_reach_the_caller()
    {
        await using var context = new PipelineContext();

        await Should.ThrowAsync<InvalidOperationException>(() => context.Dispatcher.SendAsync(
            new PlaceOrderCommand("boom", 1), TestContext.Current.CancellationToken));

        context.Logs.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Error && entry.Exception != null);
    }

    [Fact]
    public async Task The_content_of_the_request_should_never_be_logged()
    {
        await using var context = new PipelineContext();

        await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("coffee", 2, "ana@example.com"), TestContext.Current.CancellationToken);

        context.Logs.Entries.ShouldNotBeEmpty();
        context.Logs.Entries.ShouldNotContain(entry => entry.Message.Contains("ana@example.com"));
    }
}