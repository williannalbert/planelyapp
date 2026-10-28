namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>One problem of one field of the request. The Api translates <see cref="Code"/>; the field travels as it is.</summary>
public sealed record FieldError(string Field, string Code);