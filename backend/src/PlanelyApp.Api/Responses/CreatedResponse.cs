namespace PlanelyApp.Api.Responses;

/// <summary>The body of a 201: the id of what was created. The address is in the Location header.</summary>
public sealed record CreatedResponse<TId>(TId Id);