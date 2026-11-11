using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Tests.Cqrs;
using Shouldly;

namespace PlanelyApp.Application.Tests.Pipeline;

public class ValidationTests
{
    [Fact]
    public async Task A_valid_command_should_reach_the_handler()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("coffee", 2), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        context.Operations.Entries.ShouldContain("handler");
    }

    [Fact]
    public async Task Invalid_data_should_fail_with_the_fields_and_never_reach_the_handler()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("", 0), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Validation.Failed");
        Describe(result.Error).ShouldBe(
            ["Item:Validation.Required", "Quantity:Validation.OutOfRange"], ignoreOrder: true);
        context.Operations.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_above_the_maximum_should_be_reported_as_too_long()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("12345678901", 1), TestContext.Current.CancellationToken);

        Describe(result.Error).ShouldBe(["Item:Validation.TooLong"], ignoreOrder: true);
    }

    [Fact]
    public async Task The_failures_of_all_validators_should_add_up_and_a_code_set_on_the_rule_should_be_kept()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("forbidden", 0), TestContext.Current.CancellationToken);

        Describe(result.Error).ShouldBe(
            ["Quantity:Validation.OutOfRange", "Item:Order.ItemForbidden"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_rule_without_a_code_of_its_own_should_be_reported_as_invalid()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new PlaceOrderCommand("coffee", 1, "hey!"), TestContext.Current.CancellationToken);

        Describe(result.Error).ShouldBe(["Note:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_command_without_validators_should_just_run()
    {
        await using var context = new PipelineContext();

        var result = await context.Dispatcher.SendAsync(
            new GreetCommand("Ana"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("Hello, Ana!");
        context.UnitOfWork.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task A_query_should_be_validated_too()
    {
        await using var context = new PipelineContext();

        var invalid = await context.Dispatcher.QueryAsync(
            new ListOrdersQuery(""), TestContext.Current.CancellationToken);
        var valid = await context.Dispatcher.QueryAsync(
            new ListOrdersQuery("open"), TestContext.Current.CancellationToken);

        Describe(invalid.Error).ShouldBe(["Filter:Validation.Required"], ignoreOrder: true);
        valid.Value.ShouldBe(3);
    }

    private static string[] Describe(Error error) =>
        error.ShouldBeOfType<ValidationError>().Failures
            .Select(failure => $"{failure.Field}:{failure.Code}")
            .ToArray();
}