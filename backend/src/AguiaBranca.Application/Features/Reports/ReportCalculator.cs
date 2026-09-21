using System.Globalization;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Reports;

/// <summary>
/// Cálculo do dashboard como função pura (sem I/O, sem relógio próprio): porte do <c>DashboardComputer.kt</c> do app.
/// Regras herdadas do app, mantidas de propósito: período filtra ideias por <c>createdAt</c> e projetos por <c>updatedAt</c>
/// (janela [início, agora]); <c>LAST_QUARTER</c> é uma janela móvel de 3 meses (não o trimestre-calendário); a sparkline
/// tem 6 meses fixos, respeita a divisão e ignora o período; o ganho médio de produtividade só considera projetos com
/// ganho &gt; 0; "ativos" são apenas <c>EM_EXECUCAO</c>. Diferenças do app: meses/dias seguem o fuso do relatório (não o do
/// aparelho), percentuais saem com 2 casas, e empates de ordenação são resolvidos por título e id (determinístico).
/// </summary>
public static class ReportCalculator
{
    private static readonly IdeaStatus[] EvaluatedStatuses =
        [IdeaStatus.EM_ANALISE, IdeaStatus.APROVADA, IdeaStatus.REJEITADA, IdeaStatus.IMPLEMENTADA];
    private static readonly IdeaStatus[] ApprovedStatuses = [IdeaStatus.APROVADA, IdeaStatus.IMPLEMENTADA];
    private static readonly ProjectStage[] InExecutionStages = [ProjectStage.EM_EXECUCAO, ProjectStage.CONCLUIDO];

    public const int SparklineMonths = 6;

    public static ReportSummary Compute(
        IReadOnlyCollection<Idea> ideas, IReadOnlyCollection<Project> projects, IReadOnlyCollection<Guideline> guidelines,
        ReportFilters filters, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var (filteredIdeas, filteredProjects) = ApplyFilters(ideas, projects, filters, nowUtc, timeZone);
        var today = TodayIn(nowUtc, timeZone);
        var titles = guidelines.ToDictionary(g => g.Id, g => g.Title);

        var funnel = new FunnelReport(
            Submitted: filteredIdeas.Count,
            Evaluated: filteredIdeas.Count(i => EvaluatedStatuses.Contains(i.Status)),
            Approved: filteredIdeas.Count(i => ApprovedStatuses.Contains(i.Status)),
            InExecution: filteredProjects.Count(p => InExecutionStages.Contains(p.Stage)),
            RoiPositive: filteredProjects.Count(p => p.Stage == ProjectStage.CONCLUIDO && p.FinancialReturn > p.Investment));

        var totalInvestment = filteredProjects.Sum(p => p.Investment);
        var totalReturn = filteredProjects.Sum(p => p.FinancialReturn);
        var withGain = filteredProjects.Where(p => p.ProductivityGain > 0).ToList();

        var kpis = new KpisReport(
            RoiConsolidated: RoundPercent(Roi(totalReturn, totalInvestment)),
            NetProfit: totalReturn - totalInvestment,
            TotalInvestment: totalInvestment,
            TotalReturn: totalReturn,
            ActiveProjects: filteredProjects.Count(p => p.Stage == ProjectStage.EM_EXECUCAO),
            AvgProductivityGain: withGain.Count == 0 ? 0m : Round2(withGain.Sum(p => p.ProductivityGain) / withGain.Count),
            TotalCostReduction: filteredProjects.Sum(p => p.CostReduction),
            OverdueProjects: filteredProjects.Count(p => p.IsOverdue(today)));

        var guidelineImpacts = guidelines
            .Select(g =>
            {
                var gIdeas = filteredIdeas.Count(i => i.GuidelineId == g.Id);
                var gProjects = filteredProjects.Where(p => p.GuidelineId == g.Id).ToList();
                return ToImpact(g, gIdeas, gProjects);
            })
            .OrderByDescending(x => x.Report.ProjectsCount > 0)
            .ThenByDescending(x => x.Roi ?? decimal.MinValue)
            .ThenBy(x => x.Report.Title, StringComparer.Ordinal)
            .ThenBy(x => x.Report.GuidelineId, StringComparer.Ordinal)
            .Select(x => x.Report)
            .ToList();

        var projectReports = filteredProjects
            .OrderByDescending(p => p.RoiPercent ?? decimal.MinValue)
            .ThenBy(p => p.Title, StringComparer.Ordinal)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => ToProjectReport(p, titles, today))
            .ToList();

        return new ReportSummary(
            filters.Period, filters.Division, nowUtc, funnel, kpis,
            Sparkline(projects, filters.Division, nowUtc, timeZone), guidelineImpacts, projectReports);
    }

    /// <summary>Detalhe de uma orientação: ideias/projetos dela dentro dos mesmos filtros do resumo.</summary>
    public static GuidelineReportDetail ComputeGuideline(
        Guideline guideline, IReadOnlyCollection<Idea> ideas, IReadOnlyCollection<Project> projects,
        ReportFilters filters, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var (filteredIdeas, filteredProjects) = ApplyFilters(ideas, projects, filters, nowUtc, timeZone);
        var today = TodayIn(nowUtc, timeZone);
        var titles = new Dictionary<string, string> { [guideline.Id] = guideline.Title };

        var gIdeas = filteredIdeas.Where(i => i.GuidelineId == guideline.Id).ToList();
        var gProjects = filteredProjects.Where(p => p.GuidelineId == guideline.Id).ToList();
        var impact = ToImpact(guideline, gIdeas.Count, gProjects).Report;

        return new GuidelineReportDetail(
            guideline.Id, guideline.Title, guideline.Pillar, guideline.Campaign, filters.Period, filters.Division, nowUtc,
            gIdeas.Count,
            Enum.GetValues<IdeaStatus>().Select(s => new IdeaStatusCount(s, gIdeas.Count(i => i.Status == s))).ToList(),
            gIdeas.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id, StringComparer.Ordinal)
                .Select(i => new IdeaReport(i.Id, i.Title, i.Status, i.Ice?.Score, i.Division, i.CreatedAt)).ToList(),
            gProjects.Count,
            gProjects.OrderByDescending(p => p.RoiPercent ?? decimal.MinValue).ThenBy(p => p.Title, StringComparer.Ordinal)
                .ThenBy(p => p.Id, StringComparer.Ordinal).Select(p => ToProjectReport(p, titles, today)).ToList(),
            impact.Investment, impact.FinancialReturn, impact.NetProfit, impact.RoiPercent);
    }

    public static ProjectReport ToProjectReport(Project p, string? guidelineTitle, DateTime nowUtc, TimeZoneInfo timeZone) =>
        ToProjectReport(p, guidelineTitle, TodayIn(nowUtc, timeZone));

    /// <summary>Dia corrente no fuso do relatório.</summary>
    public static DateOnly TodayIn(DateTime nowUtc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(nowUtc), timeZone));

    /// <summary>Início (UTC) do dia local que contém <paramref name="nowUtc"/>.</summary>
    public static DateTime StartOfLocalDayUtc(DateTime nowUtc, TimeZoneInfo timeZone) =>
        LocalToUtc(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(nowUtc), timeZone).Date, timeZone);

    // ── filtros e janelas ────────────────────────────────────────────────────────────────────────

    private static (List<Idea> Ideas, List<Project> Projects) ApplyFilters(
        IReadOnlyCollection<Idea> ideas, IReadOnlyCollection<Project> projects, ReportFilters filters,
        DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var window = PeriodWindow(filters.Period, nowUtc, timeZone);
        bool InWindow(DateTime ts) => window is not { } w || (ts >= w.StartUtc && ts <= w.EndUtc);

        return (
            ideas.Where(i => (filters.Division is null || i.Division == filters.Division) && InWindow(i.CreatedAt)).ToList(),
            projects.Where(p => (filters.Division is null || p.Division == filters.Division) && InWindow(p.UpdatedAt)).ToList());
    }

    /// <summary><c>null</c> = sem filtro (ALL). Caso contrário [início local 00:00, agora], convertido para UTC.</summary>
    internal static (DateTime StartUtc, DateTime EndUtc)? PeriodWindow(Period period, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        if (period == Period.ALL) return null;

        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(nowUtc), timeZone);
        var startLocal = period switch
        {
            Period.THIS_MONTH => new DateTime(local.Year, local.Month, 1),
            Period.LAST_QUARTER => local.AddMonths(-3).Date,
            Period.THIS_YEAR => new DateTime(local.Year, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
        };
        return (LocalToUtc(startLocal, timeZone), AsUtc(nowUtc));
    }

    /// <summary>6 meses (do mais antigo ao corrente), por <c>updatedAt</c>, só filtrando divisão (o período não vale aqui).</summary>
    private static List<SparklinePoint> Sparkline(
        IReadOnlyCollection<Project> projects, Division? division, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var scoped = projects.Where(p => division is null || p.Division == division).ToList();
        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(nowUtc), timeZone);
        var currentMonth = new DateTime(local.Year, local.Month, 1);

        return Enumerable.Range(0, SparklineMonths)
            .Select(offset =>
            {
                var monthStart = currentMonth.AddMonths(-offset);
                var startUtc = LocalToUtc(monthStart, timeZone);
                var endUtc = LocalToUtc(monthStart.AddMonths(1), timeZone);
                var bucket = scoped.Where(p => p.UpdatedAt >= startUtc && p.UpdatedAt < endUtc).ToList();
                var roi = Roi(bucket.Sum(p => p.FinancialReturn), bucket.Sum(p => p.Investment));
                return new SparklinePoint(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), RoundPercent(roi));
            })
            .Reverse()
            .ToList();
    }

    // ── montagem ─────────────────────────────────────────────────────────────────────────────────

    private static (GuidelineImpactReport Report, decimal? Roi) ToImpact(Guideline g, int ideasCount, IReadOnlyCollection<Project> projects)
    {
        var investment = projects.Sum(p => p.Investment);
        var financialReturn = projects.Sum(p => p.FinancialReturn);
        var roi = Roi(financialReturn, investment);
        return (new GuidelineImpactReport(
            g.Id, g.Title, ideasCount, projects.Count, investment, financialReturn, financialReturn - investment, RoundPercent(roi)), roi);
    }

    private static ProjectReport ToProjectReport(Project p, IReadOnlyDictionary<string, string> titles, DateOnly today) =>
        ToProjectReport(p, p.GuidelineId is not null && titles.TryGetValue(p.GuidelineId, out var t) ? t : null, today);

    private static ProjectReport ToProjectReport(Project p, string? guidelineTitle, DateOnly today) =>
        new(p.Id, p.Title, p.Stage, p.Division, p.GuidelineId, guidelineTitle,
            p.Investment, p.FinancialReturn, p.NetProfit, RoundPercent(p.RoiPercent),
            p.ProductivityGain, p.CostReduction, p.TargetDate, p.DaysToDeadline(today), p.IsOverdue(today),
            p.StatusText, p.UpdatedAt);

    // ── números ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>ROI % = (retorno − investimento) / investimento × 100; investimento 0 → <c>null</c>.</summary>
    private static decimal? Roi(decimal financialReturn, decimal investment) =>
        investment > 0 ? (financialReturn - investment) / investment * 100m : null;

    private static decimal? RoundPercent(decimal? value) => value is { } v ? Round2(v) : null;

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Meia-noite local → UTC; horário inexistente (salto de horário de verão) avança 1 h.</summary>
    private static DateTime LocalToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified)) unspecified = unspecified.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }
}
