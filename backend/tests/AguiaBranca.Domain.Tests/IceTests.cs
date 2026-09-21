using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Tests;

public class IceTests
{
    [Fact]
    public void Score_IsProductOfThreeDimensions() =>
        new Ice(8, 7, 6).Score.Should().Be(336);

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(10, 10, 10, 1000)]
    [InlineData(1, 10, 5, 50)]
    public void Boundaries_AreAccepted(int i, int c, int e, int score) =>
        new Ice(i, c, e).Score.Should().Be(score);

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(5, 0, 5)]
    [InlineData(5, 5, 0)]
    [InlineData(11, 5, 5)]
    [InlineData(5, 11, 5)]
    [InlineData(5, 5, 11)]
    [InlineData(-1, 5, 5)]
    public void OutOfRange_IsRejected(int i, int c, int e)
    {
        var act = () => new Ice(i, c, e);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.ValidationError);
        Ice.IsValid(i, c, e).Should().BeFalse();
    }

    [Fact]
    public void Equality_IsByValue() =>
        new Ice(3, 4, 5).Should().Be(new Ice(3, 4, 5));
}
