namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>What kind of failure it is. The Api maps each one to an HTTP status.</summary>
public enum ErrorType
{
    /// <summary>The data of the request is malformed (400).</summary>
    Validation,

    /// <summary>What the request points to does not exist, or is not visible to this company (404).</summary>
    NotFound,

    /// <summary>The request clashes with the current state (409).</summary>
    Conflict,

    /// <summary>The user is known but may not do this (403).</summary>
    Forbidden,
}