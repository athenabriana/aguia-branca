using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Entities;

/// <summary>Entrada imutável do histórico de um projeto (R-04.5).</summary>
public sealed class ProjectUpdate
{
    public string Id { get; private set; } = string.Empty;
    public string ProjectId { get; private set; } = string.Empty;
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string Note { get; private set; } = string.Empty;
    public List<FieldChange> Changes { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }

    private ProjectUpdate() { }

    public static ProjectUpdate Create(
        string projectId, string authorId, string authorName, string? note,
        IEnumerable<FieldChange> changes, DateTime now) =>
        new()
        {
            Id = EntityId.New(),
            ProjectId = projectId,
            AuthorId = authorId,
            AuthorName = authorName,
            Note = (note ?? string.Empty).Trim(),
            Changes = changes.ToList(),
            CreatedAt = now
        };
}
