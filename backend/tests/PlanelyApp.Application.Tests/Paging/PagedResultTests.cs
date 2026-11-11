using PlanelyApp.Application.Abstractions.Paging;
using Shouldly;

namespace PlanelyApp.Application.Tests.Paging;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(40, 20, 2)]
    [InlineData(41, 20, 3)]
    public void The_pages_should_cover_the_whole_total(int totalCount, int pageSize, int expectedPages)
    {
        var result = new PagedResult<int>([], 1, pageSize, totalCount);

        result.TotalPages.ShouldBe(expectedPages);
    }
}