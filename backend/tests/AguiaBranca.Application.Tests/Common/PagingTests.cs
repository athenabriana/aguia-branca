using AguiaBranca.Application.Common.Paging;

namespace AguiaBranca.Application.Tests.Common;

public class PagingTests
{
    [Fact]
    public void Defaults_Are_Page1_Size50() =>
        new PageRequest().Should().Be(new PageRequest(1, 50));

    [Theory]
    [InlineData(1, 50, 0)]
    [InlineData(2, 50, 50)]
    [InlineData(3, 10, 20)]
    public void Skip_IsComputed(int page, int size, int skip) =>
        new PageRequest(page, size).Skip.Should().Be(skip);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 200)]
    public void Valid_Bounds_HaveNoErrors(int page, int size) =>
        new PageRequest(page, size).Validate().Should().BeEmpty();

    [Theory]
    [InlineData(0, 50, "page")]
    [InlineData(-3, 50, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 201, "pageSize")]
    public void Invalid_Bounds_ReportField(int page, int size, string field) =>
        new PageRequest(page, size).Validate().Should().ContainSingle(e => e.Field == field);

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(1, 50, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(401, 200, 3)]
    public void TotalPages_RoundsUp(int total, int size, int pages) =>
        new PagedResult<int>([], 1, size, total).TotalPages.Should().Be(pages);

    [Fact]
    public void Map_PreservesPaging()
    {
        var mapped = new PagedResult<int>([1, 2], 2, 2, 5).Map(x => $"#{x}");
        mapped.Items.Should().Equal("#1", "#2");
        (mapped.Page, mapped.PageSize, mapped.TotalItems, mapped.TotalPages).Should().Be((2, 2, 5, 3));
    }
}
