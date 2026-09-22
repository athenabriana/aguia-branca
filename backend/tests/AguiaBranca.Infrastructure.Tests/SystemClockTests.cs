using AguiaBranca.Infrastructure.Time;

namespace AguiaBranca.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_IsUtc_AndTruncatedToMilliseconds()
    {
        var clock = new SystemClock();
        for (var i = 0; i < 200; i++)
        {
            var now = clock.UtcNow;
            now.Kind.Should().Be(DateTimeKind.Utc);
            (now.Ticks % TimeSpan.TicksPerMillisecond).Should().Be(0, "o BSON DateTime tem precisão de milissegundo");
        }
    }

    [Fact]
    public void UtcNow_TracksTheRealClock() =>
        new SystemClock().UtcNow.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
