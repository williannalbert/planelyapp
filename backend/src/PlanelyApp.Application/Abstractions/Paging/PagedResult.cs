namespace PlanelyApp.Application.Abstractions.Paging;

/// <summary>One page of a list, with the size of the whole list so the client can draw the pages.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}