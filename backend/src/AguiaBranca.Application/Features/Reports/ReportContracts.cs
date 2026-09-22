using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Reports;

/// <summary>Filtros do dashboard. O período afeta funil/KPIs/impacto/lista; a divisão afeta também a sparkline.</summary>
public sealed record ReportFilters(Period Period = Period.ALL, Division? Division = null);

public sealed record FunnelReport(int Submitted, int Evaluated, int Approved, int InExecution, int RoiPositive);

/// <summary>Percentuais com 2 casas (arredondamento comercial); valores monetários em BRL, sem arredondar.</summary>
public sealed record KpisReport(
    decimal? RoiConsolidated, decimal NetProfit, decimal TotalInvestment, decimal TotalReturn, int ActiveProjects,
    decimal AvgProductivityGain, decimal TotalCostReduction, int OverdueProjects);

/// <summary>Um mês da sparkline (<c>Month</c> = "yyyy-MM"); <c>RoiPercent</c> nulo quando o investimento do mês é 0.</summary>
public sealed record SparklinePoint(string Month, decimal? RoiPercent);

public sealed record GuidelineImpactReport(
    string GuidelineId, string Title, int IdeasCount, int ProjectsCount,
    decimal Investment, decimal FinancialReturn, decimal NetProfit, decimal? RoiPercent);

public sealed record ProjectReport(
    string Id, string Title, ProjectStage Stage, Division Division, string? GuidelineId, string? GuidelineTitle,
    decimal Investment, decimal FinancialReturn, decimal NetProfit, decimal? RoiPercent,
    decimal ProductivityGain, decimal CostReduction, DateTime? TargetDate, int? DaysToDeadline, bool Overdue,
    string StatusText, DateTime UpdatedAt);

public sealed record ReportSummary(
    Period Period, Division? Division, DateTime GeneratedAt,
    FunnelReport Funnel, KpisReport Kpis, IReadOnlyList<SparklinePoint> Sparkline,
    IReadOnlyList<GuidelineImpactReport> GuidelineImpacts, IReadOnlyList<ProjectReport> Projects);

public sealed record IdeaStatusCount(IdeaStatus Status, int Count);

public sealed record IdeaReport(string Id, string Title, IdeaStatus Status, int? IceScore, Division Division, DateTime CreatedAt);

/// <summary>Retorno de uma orientação estratégica: ideias e projetos ligados a ela dentro dos filtros.</summary>
public sealed record GuidelineReportDetail(
    string Id, string Title, Pillar Pillar, string? Campaign, Period Period, Division? Division, DateTime GeneratedAt,
    int IdeasCount, IReadOnlyList<IdeaStatusCount> IdeasByStatus, IReadOnlyList<IdeaReport> Ideas,
    int ProjectsCount, IReadOnlyList<ProjectReport> Projects,
    decimal Investment, decimal FinancialReturn, decimal NetProfit, decimal? RoiPercent);

public sealed record GuidelineReportList(
    Period Period, Division? Division, DateTime GeneratedAt, IReadOnlyList<GuidelineImpactReport> Items);

public sealed record GetReportSummaryQuery(Period Period = Period.ALL, Division? Division = null);
public sealed record ListGuidelineReportsQuery(Period Period = Period.ALL, Division? Division = null);
public sealed record GetGuidelineReportQuery(string Id, Period Period = Period.ALL, Division? Division = null);
public sealed record GetProjectReportQuery(string Id);

internal static class ReportErrors
{
    public static readonly Error GuidelineNotFound = Error.NotFound("Orientação não encontrada.");
    public static readonly Error ProjectNotFound = Error.NotFound("Projeto não encontrado.");
}
