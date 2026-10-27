namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>"No value": the response of a command that has nothing to return but whether it worked.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}