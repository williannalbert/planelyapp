namespace PlanelyApp.Application.Abstractions.Paging;

public static class PageLimits
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    /// <summary>Keeps the offset of a page small enough to be cheap and to fit an int.</summary>
    public const int MaxPage = 10_000;
}