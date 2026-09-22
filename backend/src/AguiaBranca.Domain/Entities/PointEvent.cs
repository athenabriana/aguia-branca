using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Domain.Entities;

/// <summary>Razão imutável de pontos. <see cref="Delta"/> é o valor <b>efetivo</b> (após o clamp em 0).</summary>
public sealed class PointEvent
{
    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public int Delta { get; private set; }
    public PointReason Reason { get; private set; }
    public string? RefId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PointEvent() { }

    public static PointEvent Create(string userId, int effectiveDelta, PointReason reason, string? refId, DateTime now) =>
        new() { Id = EntityId.New(), UserId = userId, Delta = effectiveDelta, Reason = reason, RefId = refId, CreatedAt = now };
}
