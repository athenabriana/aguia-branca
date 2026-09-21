using AguiaBranca.Domain.Common;

namespace AguiaBranca.Domain.Tests;

public class EntityIdTests
{
    [Fact]
    public void New_IsValid24LowerHex()
    {
        var id = EntityId.New();
        id.Should().MatchRegex("^[0-9a-f]{24}$");
        EntityId.IsValid(id).Should().BeTrue();
    }

    [Fact]
    public void New_IsUniqueAndSortableByTime()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => EntityId.New()).ToList();
        ids.Distinct().Should().HaveCount(1000);

        var early = EntityId.New(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var late = EntityId.New(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        string.CompareOrdinal(early, late).Should().BeNegative();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("123", false)]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz", false)]
    [InlineData("665f00000000000000000001", true)]
    [InlineData("665F00000000000000000001", true)]
    public void IsValid_RecognizesObjectIdFormat(string? value, bool expected) =>
        EntityId.IsValid(value).Should().Be(expected);
}
