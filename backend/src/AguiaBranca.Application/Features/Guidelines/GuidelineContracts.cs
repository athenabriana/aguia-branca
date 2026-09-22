using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Guidelines;

public sealed record GuidelineResponse(
    string Id, string Title, string Description, Pillar Pillar, string? Campaign,
    string AuthorId, string AuthorName, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static GuidelineResponse From(Guideline g) =>
        new(g.Id, g.Title, g.Description, g.Pillar, g.Campaign, g.AuthorId, g.AuthorName, g.CreatedAt, g.UpdatedAt);
}

public sealed record GuidelineSnapshotResponse(string Title, string Description, Pillar Pillar, string? Campaign);

/// <summary>Registro histórico (R2-02.4): id, data (<c>occurredAt</c>), categoria (= pilar) e campanha.</summary>
public sealed record GuidelineHistoryResponse(
    string Id, string GuidelineId, DateTime OccurredAt, Pillar Category, string? Campaign,
    GuidelineAction Action, string Title, GuidelineSnapshotResponse Snapshot, string ChangedById, string ChangedByName)
{
    public static GuidelineHistoryResponse From(GuidelineHistoryEntry e) =>
        new(e.Id, e.GuidelineId, e.OccurredAt, e.Category, e.Campaign, e.Action, e.Title,
            new GuidelineSnapshotResponse(e.Snapshot.Title, e.Snapshot.Description, e.Snapshot.Pillar, e.Snapshot.Campaign),
            e.ChangedById, e.ChangedByName);
}

// Campos anuláveis: a validação (mensagens em pt-BR) é feita pelo FluentValidation, não pelo binding do MVC.
public sealed record CreateGuidelineCommand(string? Title, string? Description, Pillar? Pillar, string? Campaign);
public sealed record UpdateGuidelineCommand(string Id, string? Title, string? Description, Pillar? Pillar, string? Campaign);
public sealed record DeleteGuidelineCommand(string Id);
public sealed record GetGuidelineQuery(string Id);
public sealed record ListGuidelinesQuery(int Page = 1, int PageSize = 50);
public sealed record GuidelineHistoryQuery(
    string? GuidelineId = null, Pillar? Category = null, string? Campaign = null,
    DateTime? From = null, DateTime? To = null, int Page = 1, int PageSize = 50);

internal static class GuidelineErrors
{
    public static readonly Error NotFound = Error.NotFound("Orientação não encontrada.");
}
