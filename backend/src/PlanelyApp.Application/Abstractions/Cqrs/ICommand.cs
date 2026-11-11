namespace PlanelyApp.Application.Abstractions.Cqrs;

/// <summary>A request to change something. <typeparamref name="TResponse"/> is <c>Unit</c> when there is nothing to return.</summary>
public interface ICommand<TResponse>;