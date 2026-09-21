using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Entities;

/// <summary>Campos editáveis de um projeto (entrada de criação/edição).</summary>
public sealed record ProjectData(
    string Title,
    string Description,
    ProjectStage Stage,
    string StatusText,
    decimal Investment,
    DateTime? TargetDate,
    decimal FinancialReturn,
    decimal ProductivityGain,
    decimal CostReduction,
    Division Division,
    string? GuidelineId,
    string? ResponsibleId = null,
    string? ResponsibleName = null);

/// <summary>Resultado de uma edição: diff dos campos e se o projeto acabou de ser concluído.</summary>
public sealed record ProjectUpdateOutcome(IReadOnlyList<FieldChange> Changes, bool BecameCompleted);

public sealed class Project
{
    public const int TitleMax = 200, DescriptionMax = 4000, StatusTextMax = 200;
    public const string DraftPrefix = "PROJ: ";

    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ProjectStage Stage { get; private set; }
    public string StatusText { get; private set; } = string.Empty;
    public decimal Investment { get; private set; }
    public DateTime? TargetDate { get; private set; }
    public decimal FinancialReturn { get; private set; }
    public decimal ProductivityGain { get; private set; }
    public decimal CostReduction { get; private set; }
    public Division Division { get; private set; }
    public string? GuidelineId { get; private set; }
    public string CreatorManagerId { get; private set; } = string.Empty;
    public string? OriginatingIdeaId { get; private set; }
    public int? PriorityScore { get; private set; }
    public string? ReporterId { get; private set; }
    public string? ReporterName { get; private set; }
    public string? ResponsibleId { get; private set; }
    public string? ResponsibleName { get; private set; }
    public int Version { get; private set; }
    public string? LegacyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Project() { }

    public decimal NetProfit => FinancialReturn - Investment;

    /// <summary>ROI % = (retorno − investimento) / investimento × 100; <c>null</c> quando o investimento é 0.</summary>
    public decimal? RoiPercent => Investment > 0 ? NetProfit / Investment * 100m : null;

    public bool IsOverdue(DateTime now) =>
        TargetDate is { } target && target < now && Stage is not (ProjectStage.CONCLUIDO or ProjectStage.CANCELADO);

    public static Project Create(
        ProjectData data, string creatorManagerId, string creatorManagerName, DateTime now,
        string? originatingIdeaId = null, string? legacyId = null)
    {
        var project = new Project
        {
            Id = EntityId.New(),
            CreatorManagerId = creatorManagerId,
            OriginatingIdeaId = originatingIdeaId,
            LegacyId = legacyId,
            CreatedAt = now,
            Version = 1
        };
        project.Assign(data with
        {
            ResponsibleId = data.ResponsibleId ?? creatorManagerId,
            ResponsibleName = data.ResponsibleId is null ? creatorManagerName : data.ResponsibleName
        }, now);
        return project;
    }

    /// <summary>Rascunho criado na aprovação da ideia (R-03.8 / R2-03.8).</summary>
    public static Project CreateDraftFromIdea(Idea idea, string reviewerId, string reviewerName, DateTime now)
    {
        var project = Create(
            new ProjectData(
                Title: DraftPrefix + idea.Title,
                Description: idea.Description,
                Stage: ProjectStage.PLANEJAMENTO,
                StatusText: "Iniciado a partir de aprovação",
                Investment: 0m,
                TargetDate: null,
                FinancialReturn: 0m,
                ProductivityGain: 0m,
                CostReduction: 0m,
                Division: idea.Division,
                GuidelineId: idea.GuidelineId,
                ResponsibleId: reviewerId,
                ResponsibleName: reviewerName),
            creatorManagerId: reviewerId,
            creatorManagerName: reviewerName,
            now: now,
            originatingIdeaId: idea.Id);

        project.PriorityScore = idea.Ice?.Score;
        project.ReporterId = idea.AuthorId;
        project.ReporterName = idea.AuthorName;
        return project;
    }

    /// <summary>
    /// Aplica a edição, calcula o diff (só campos realmente alterados) e incrementa a versão.
    /// Se <paramref name="expectedVersion"/> for informada e divergir, lança CONCURRENCY_CONFLICT.
    /// </summary>
    public ProjectUpdateOutcome ApplyUpdate(ProjectData data, DateTime now, int? expectedVersion = null)
    {
        if (expectedVersion is { } expected && expected != Version)
            throw new DomainException(DomainErrorCodes.ConcurrencyConflict,
                "O projeto foi alterado por outra pessoa. Recarregue e tente novamente.");

        var previousStage = Stage;
        var changes = new List<FieldChange>();
        var title = data.Title?.Trim() ?? string.Empty;
        var description = data.Description?.Trim() ?? string.Empty;
        var statusText = data.StatusText?.Trim() ?? string.Empty;

        void Text(string field, string? from, string? to) { if (from != to) changes.Add(FieldChange.Text(field, from, to)); }
        void Number(string field, decimal from, decimal to) { if (from != to) changes.Add(FieldChange.Number(field, from, to)); }

        Text("title", Title, title);
        Text("description", Description, description);
        Text("stage", Stage.ToString(), data.Stage.ToString());
        Text("statusText", StatusText, statusText);
        Number("investment", Investment, data.Investment);
        Number("financialReturn", FinancialReturn, data.FinancialReturn);
        Number("productivityGain", ProductivityGain, data.ProductivityGain);
        Number("costReduction", CostReduction, data.CostReduction);
        Text("division", Division.ToString(), data.Division.ToString());
        Text("guidelineId", GuidelineId, data.GuidelineId);
        if (TargetDate != data.TargetDate) changes.Add(FieldChange.Date("targetDate", TargetDate, data.TargetDate));

        var responsibleChanged = data.ResponsibleId is not null && data.ResponsibleId != ResponsibleId;
        if (responsibleChanged)
            changes.Add(FieldChange.Text("responsável", ResponsibleName ?? "—", data.ResponsibleName ?? "—"));

        Assign(data with
        {
            ResponsibleId = responsibleChanged ? data.ResponsibleId : ResponsibleId,
            ResponsibleName = responsibleChanged ? data.ResponsibleName : ResponsibleName
        }, now);
        Version++;

        var completed = previousStage != ProjectStage.CONCLUIDO && Stage == ProjectStage.CONCLUIDO;
        return new ProjectUpdateOutcome(changes, completed);
    }

    private void Assign(ProjectData data, DateTime now)
    {
        var title = data.Title?.Trim() ?? string.Empty;
        var description = data.Description?.Trim() ?? string.Empty;
        var statusText = data.StatusText?.Trim() ?? string.Empty;

        if (title.Length == 0 || title.Length > TitleMax)
            throw DomainException.Validation(nameof(data.Title), $"Título é obrigatório e deve ter até {TitleMax} caracteres.");
        if (description.Length > DescriptionMax)
            throw DomainException.Validation(nameof(data.Description), $"Descrição deve ter no máximo {DescriptionMax} caracteres.");
        if (statusText.Length > StatusTextMax)
            throw DomainException.Validation(nameof(data.StatusText), $"Status deve ter no máximo {StatusTextMax} caracteres.");
        NonNegative(nameof(data.Investment), data.Investment);
        NonNegative(nameof(data.FinancialReturn), data.FinancialReturn);
        NonNegative(nameof(data.ProductivityGain), data.ProductivityGain);
        NonNegative(nameof(data.CostReduction), data.CostReduction);
        if (data.GuidelineId is not null && !EntityId.IsValid(data.GuidelineId))
            throw DomainException.Validation(nameof(data.GuidelineId), "Identificador de orientação inválido.");

        Title = title;
        Description = description;
        Stage = data.Stage;
        StatusText = statusText;
        Investment = data.Investment;
        TargetDate = data.TargetDate;
        FinancialReturn = data.FinancialReturn;
        ProductivityGain = data.ProductivityGain;
        CostReduction = data.CostReduction;
        Division = data.Division;
        GuidelineId = data.GuidelineId;
        ResponsibleId = data.ResponsibleId;
        ResponsibleName = data.ResponsibleName;
        UpdatedAt = now;
    }

    private static void NonNegative(string field, decimal value)
    {
        if (value < 0) throw DomainException.Validation(field, $"'{field}' não pode ser negativo.");
    }
}
