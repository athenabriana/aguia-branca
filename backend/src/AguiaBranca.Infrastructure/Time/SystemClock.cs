using AguiaBranca.Application.Common.Abstractions;

namespace AguiaBranca.Infrastructure.Time;

/// <summary>
/// Relógio do sistema, <b>truncado para milissegundos</b>: é a precisão do BSON DateTime. Assim o valor devolvido na
/// resposta de uma criação é idêntico ao que será lido do banco depois.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTime UtcNow
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        }
    }
}
