using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using Shouldly;

namespace PlanelyApp.Application.Tests.Cqrs;

public class DispatcherTests
{
    [Fact]
    public async Task A_command_should_reach_its_handler_and_return_its_value()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();

        var result = await DispatcherOf(scope).SendAsync(new GreetCommand("Ana"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Hello, Ana!");
    }

    [Fact]
    public async Task The_failure_of_the_handler_should_come_back_as_it_is()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();

        var result = await DispatcherOf(scope).SendAsync(new GreetCommand(" "), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Greeting.NameRequired");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Different_commands_should_reach_different_handlers()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = DispatcherOf(scope);

        var greeting = await dispatcher.SendAsync(new GreetCommand("Ana"), TestContext.Current.CancellationToken);
        var count = await dispatcher.SendAsync(new CountCommand(9), TestContext.Current.CancellationToken);
        var greetingAgain = await dispatcher.SendAsync(new GreetCommand("Bia"), TestContext.Current.CancellationToken);

        greeting.Value.ShouldBe("Hello, Ana!");
        count.Value.ShouldBe(10);
        greetingAgain.Value.ShouldBe("Hello, Bia!");
    }

    [Fact]
    public async Task A_query_should_reach_its_query_handler()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = DispatcherOf(scope);

        var found = await dispatcher.QueryAsync(new GreetingQuery("Ana"), TestContext.Current.CancellationToken);
        var missing = await dispatcher.QueryAsync(new GreetingQuery("nobody"), TestContext.Current.CancellationToken);

        found.Value.ShouldBe("Hi, Ana.");
        missing.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task A_command_without_a_value_should_return_unit()
    {
        var handler = new RecordingHandler();
        await using var provider = Build(services =>
            services.AddSingleton<ICommandHandler<RecordCommand, Unit>>(handler));
        await using var scope = provider.CreateAsyncScope();

        var result = await DispatcherOf(scope).SendAsync(new RecordCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task The_cancellation_token_should_reach_the_handler()
    {
        var handler = new RecordingHandler();
        await using var provider = Build(services =>
            services.AddSingleton<ICommandHandler<RecordCommand, Unit>>(handler));
        await using var scope = provider.CreateAsyncScope();
        using var source = new CancellationTokenSource();

        await DispatcherOf(scope).SendAsync(new RecordCommand(), source.Token);

        handler.ReceivedToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Handlers_should_be_resolved_from_the_scope_of_the_dispatcher()
    {
        await using var provider = Build();
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await DispatcherOf(firstScope).SendAsync(new WhoAmICommand(), cancellationToken);
        var firstAgain = await DispatcherOf(firstScope).SendAsync(new WhoAmICommand(), cancellationToken);
        var second = await DispatcherOf(secondScope).SendAsync(new WhoAmICommand(), cancellationToken);

        firstAgain.Value.ShouldBe(first.Value);
        second.Value.ShouldNotBe(first.Value);
    }

    [Fact]
    public async Task A_command_without_a_handler_should_fail_loudly()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = DispatcherOf(scope);

        // Nobody registered a handler for this one: a configuration mistake, not an outcome to handle.
        await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new OrphanCommand(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_null_request_should_be_refused()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = DispatcherOf(scope);

        await Should.ThrowAsync<ArgumentNullException>(
            () => dispatcher.SendAsync<string>(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(
            () => dispatcher.QueryAsync<string>(null!, TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Build(Action<IServiceCollection>? register = null)
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddScoped<ScopeMarker>();
        services.AddScoped<ICommandHandler<GreetCommand, string>, GreetHandler>();
        services.AddScoped<ICommandHandler<CountCommand, int>, CountHandler>();
        services.AddScoped<ICommandHandler<WhoAmICommand, Guid>, WhoAmIHandler>();
        services.AddScoped<IQueryHandler<GreetingQuery, string>, GreetingQueryHandler>();
        register?.Invoke(services);

        // Validating scopes proves the dispatcher is scoped: asking for it from the root would throw.
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IDispatcher DispatcherOf(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IDispatcher>();

    private sealed record OrphanCommand : ICommand<string>;
}