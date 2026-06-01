using umbral_backend.Application.Common.Models;

namespace umbral_backend.Application.UnitTests.Common.Models;

public class PagedResultTests
{
    [Fact]
    public void TotalPages_RoundsUpFractionalPage()
    {
        var paged = new PagedResult<string>
        {
            Items = new List<string>(),
            TotalCount = 55,
            Page = 1,
            PageSize = 10
        };

        paged.TotalPages.Should().Be(6);
        paged.HasPreviousPage.Should().BeFalse();
        paged.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenOnLastPage_ReturnsFalse()
    {
        var paged = new PagedResult<string>
        {
            Items = new List<string>(),
            TotalCount = 30,
            Page = 3,
            PageSize = 10
        };

        paged.TotalPages.Should().Be(3);
        paged.HasNextPage.Should().BeFalse();
        paged.HasPreviousPage.Should().BeTrue();
    }
}
