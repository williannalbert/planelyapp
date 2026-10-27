namespace PlanelyApp.Application.Abstractions.Cqrs;

/// <summary>A request to read something. It never changes state.</summary>
public interface IQuery<TResponse>;