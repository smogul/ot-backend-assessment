using OT.Assessment.Application.Dtos;

namespace OT.Assessment.UnitTests;

public class PagedResultTests
{
    /// 35 stories at 10 per page need 4 pages, as in the README's example.
    [Fact]
    public void Create_RoundsUp_WhenItemsDoNotFillTheLastPage()
    {
        var result = PagedResult<string>.Create(["a"], page: 1, pageSize: 10, total: 35);

        Assert.Equal(4, result.TotalPages);
    }

    /// A new player with 0 wagers has 0 pages, not 1 empty page.
    [Fact]
    public void Create_ReturnsZeroPages_WhenThereAreNoItems()
    {
        var result = PagedResult<string>.Create([], page: 1, pageSize: 10, total: 0);

        Assert.Equal(0, result.TotalPages);
    }
}
