using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Ideas;

public sealed record IceResponse(int Impact, int Confidence, int Ease, int Score);

/// <summary>Resumo do projeto criado a partir da ideia — permite ao operador montar a jornada sem acessar <c>/projects</c> (R2-03.9).</summary>
public sealed record LinkedProjectResponse(string Id, ProjectStage Stage, DateTime UpdatedAt);

public sealed record IdeaResponse(
    string Id, string Title, string Description, string Category, Division Division,
    string? GuidelineId, string? GuidelineTitle,
    string AuthorId, string AuthorName, IdeaStatus Status, IceResponse? Ice,
    string? ReviewerId, string? ReviewComment,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ReviewedAt,
    LinkedProjectResponse? LinkedProject, int? PointsAwarded)
{
    public static IdeaResponse From(Idea i, string? guidelineTitle, LinkedProjectResponse? linkedProject, int? pointsAwarded = null) =>
        new(i.Id, i.Title, i.Description, i.Category, i.Division, i.GuidelineId, guidelineTitle,
            i.AuthorId, i.AuthorName, i.Status,
            i.Ice is null ? null : new IceResponse(i.Ice.Impact, i.Ice.Confidence, i.Ice.Ease, i.Ice.Score),
            i.ReviewerId, i.ReviewComment, i.CreatedAt, i.UpdatedAt, i.ReviewedAt, linkedProject, pointsAwarded);
}

public enum IdeaScope { MINE, CURATION, ALL }

// Campos anuláveis: a validação (mensagens em pt-BR) é do FluentValidation.
public sealed record CreateIdeaCommand(string? Title, string? Description, string? Category, Division? Division, string? GuidelineId);
public sealed record UpdateIdeaCommand(string Id, string? Title, string? Description, string? Category, Division? Division, string? GuidelineId);
public sealed record DeleteIdeaCommand(string Id);
public sealed record GetIdeaQuery(string Id);
public sealed record ListIdeasQuery(
    IdeaScope? Scope = null, IdeaStatus? Status = null, string? GuidelineId = null, Division? Division = null,
    int Page = 1, int PageSize = 50);

public sealed record SaveIceCommand(string Id, int? Impact, int? Confidence, int? Ease);
public sealed record RejectIdeaCommand(string Id, string? Comment);
public sealed record ApproveIdeaCommand(string Id);

/// <summary>Resultado da aprovação. <c>AlreadyApproved</c> = a ideia já estava aprovada (chamada idempotente).</summary>
public sealed record ApproveIdeaResult(string IdeaId, string? ProjectId, bool AlreadyApproved);

internal static class IdeaErrors
{
    public static readonly Error NotFound = Error.NotFound("Ideia não encontrada.");
    public static readonly Error Forbidden = Error.Forbidden("Você não pode alterar esta ideia.");
    public static readonly Error UserNotFound = Error.Unauthorized("Usuário não encontrado.");
    public static Error GuidelineNotFound() =>
        Error.Unprocessable("GUIDELINE_NOT_FOUND", "A orientação estratégica informada não existe.", "guidelineId");
}
