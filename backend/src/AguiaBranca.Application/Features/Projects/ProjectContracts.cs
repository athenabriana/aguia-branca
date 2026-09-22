using System.Globalization;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Features.Projects;

public sealed record ProjectResponse(
    string Id, string Title, string Description, ProjectStage Stage, string StatusText,
    decimal Investment, DateTime? TargetDate, decimal FinancialReturn, decimal ProductivityGain, decimal CostReduction,
    Division Division, string? GuidelineId, string? GuidelineTitle,
    string CreatorManagerId, string? OriginatingIdeaId, int? PriorityScore,
    string? ReporterId, string? ReporterName, string? ResponsibleId, string? ResponsibleName,
    decimal NetProfit, decimal? RoiPercent, int Version, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ProjectResponse From(Project p, string? guidelineTitle) =>
        new(p.Id, p.Title, p.Description, p.Stage, p.StatusText,
            p.Investment, p.TargetDate, p.FinancialReturn, p.ProductivityGain, p.CostReduction,
            p.Division, p.GuidelineId, guidelineTitle,
            p.CreatorManagerId, p.OriginatingIdeaId, p.PriorityScore,
            p.ReporterId, p.ReporterName, p.ResponsibleId, p.ResponsibleName,
            p.NetProfit, p.RoiPercent, p.Version, p.CreatedAt, p.UpdatedAt);
}

/// <summary>Diferença de um campo. <c>From</c>/<c>To</c> saem tipados no JSON: número, texto (ou data ISO) e <c>null</c>.</summary>
public sealed record FieldChangeResponse(string Field, object? From, object? To)
{
    public static FieldChangeResponse Of(FieldChange c) => new(c.Field, Convert(c.Kind, c.From), Convert(c.Kind, c.To));

    private static object? Convert(FieldValueKind kind, string? value) => value switch
    {
        null => null,
        _ when kind == FieldValueKind.NUMBER => decimal.Parse(value, CultureInfo.InvariantCulture),
        _ => value
    };
}

public sealed record ProjectUpdateResponse(
    string Id, string ProjectId, string AuthorId, string AuthorName, string Note,
    IReadOnlyList<FieldChangeResponse> Changes, DateTime CreatedAt)
{
    public static ProjectUpdateResponse From(ProjectUpdate u) =>
        new(u.Id, u.ProjectId, u.AuthorId, u.AuthorName, u.Note, u.Changes.Select(FieldChangeResponse.Of).ToList(), u.CreatedAt);
}

// Campos anuláveis: a validação (mensagens em pt-BR) é do FluentValidation.
public sealed record CreateProjectCommand(
    string? Title, string? Description, ProjectStage? Stage, string? StatusText,
    decimal? Investment, DateTime? TargetDate, decimal? FinancialReturn, decimal? ProductivityGain, decimal? CostReduction,
    Division? Division, string? GuidelineId, string? ResponsibleId);

/// <summary>PUT é uma substituição completa: estágio, divisão e valores são obrigatórios (evita zerar campos por omissão).</summary>
public sealed record UpdateProjectCommand(
    string Id, string? Title, string? Description, ProjectStage? Stage, string? StatusText,
    decimal? Investment, DateTime? TargetDate, decimal? FinancialReturn, decimal? ProductivityGain, decimal? CostReduction,
    Division? Division, string? GuidelineId, string? ResponsibleId, string? Note, int? Version);

public sealed record DeleteProjectCommand(string Id);
public sealed record GetProjectQuery(string Id);
public sealed record ListProjectsQuery(ProjectStage? Stage = null, Division? Division = null, string? GuidelineId = null, int Page = 1, int PageSize = 50);
public sealed record ListProjectUpdatesQuery(string ProjectId, int Page = 1, int PageSize = 50);

internal static class ProjectErrors
{
    public static readonly Error NotFound = Error.NotFound("Projeto não encontrado.");
    public static Error GuidelineNotFound() =>
        Error.Unprocessable("GUIDELINE_NOT_FOUND", "A orientação estratégica informada não existe.", "guidelineId");
    public static Error ResponsibleNotFound() =>
        Error.Unprocessable("RESPONSIBLE_NOT_FOUND", "O responsável informado não existe ou não é gestor/líder.", "responsibleId");
    public static Error Concurrency(string message) => Error.Conflict("CONCURRENCY_CONFLICT", message);
}
