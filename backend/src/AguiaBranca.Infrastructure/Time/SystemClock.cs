using AguiaBranca.Application.Common.Abstractions;

namespace AguiaBranca.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
