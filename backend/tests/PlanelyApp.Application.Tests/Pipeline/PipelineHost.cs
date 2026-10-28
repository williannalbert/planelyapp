using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Decorators;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Tests.Pipeline;

/// <summary>The real pipeline (scanning and decorators) over the sample handlers, with fakes where the Infrastructure would be.</summary>
internal static class PipelineHost
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddDispatching();
        services.AddUseCases(typeof(PipelineHost).Assembly);

        services.AddSingleton<OperationLog>();
        services.AddSingleton<FakeDomainEventSource>();
        services.AddSingleton<IDomainEventSource>(provider => provider.GetRequiredService<FakeDomainEventSource>());
        services.AddSingleton<FakeUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<FakeUnitOfWork>());
        services.AddSingleton<ListLogger<UseCase>>();
        services.AddSingleton<ILogger<UseCase>>(provider => provider.GetRequiredService<ListLogger<UseCase>>());

        return services.BuildServiceProvider(validateScopes: true);
    }
}

/// <summary>One request's worth of the pipeline: a scope, the dispatcher and the fakes to look at afterwards.</summary>
internal sealed class PipelineContext : IAsyncDisposable
{
    private readonly ServiceProvider _provider = PipelineHost.Build();
    private readonly AsyncServiceScope _scope;

    public PipelineContext() => _scope = _provider.CreateAsyncScope();

    public IDispatcher Dispatcher => _scope.ServiceProvider.GetRequiredService<IDispatcher>();

    public OperationLog Operations => _provider.GetRequiredService<OperationLog>();

    public FakeUnitOfWork UnitOfWork => _provider.GetRequiredService<FakeUnitOfWork>();

    public ListLogger<UseCase> Logs => _provider.GetRequiredService<ListLogger<UseCase>>();

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}