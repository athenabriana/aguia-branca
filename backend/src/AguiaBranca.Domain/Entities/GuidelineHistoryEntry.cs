using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Domain.Entities;

/// <summary>Registro histórico imutável de uma orientação (R2-02.4): id, data, categoria (= pilar), campanha.</summary>
public sealed class GuidelineHistoryEntry
{
    public string Id { get; private set; } = string.Empty;
    public string GuidelineId { get; private set; } = string.Empty;
    public GuidelineAction Action { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public Pillar Category { get; private set; }
    public string? Campaign { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public GuidelineSnapshot Snapshot { get; private set; } = null!;
    public string ChangedById { get; private set; } = string.Empty;
    public string ChangedByName { get; private set; } = string.Empty;

    private GuidelineHistoryEntry() { }

    public static GuidelineHistoryEntry From(
        Guideline guideline, GuidelineAction action, string changedById, string changedByName, DateTime now) =>
        new()
        {
            Id = EntityId.New(),
            GuidelineId = guideline.Id,
            Action = action,
            OccurredAt = now,
            Category = guideline.Pillar,
            Campaign = guideline.Campaign,
            Title = guideline.Title,
            Snapshot = guideline.ToSnapshot(),
            ChangedById = changedById,
            ChangedByName = changedByName
        };
}
