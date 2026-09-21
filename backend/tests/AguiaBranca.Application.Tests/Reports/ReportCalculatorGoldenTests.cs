using System.Text.Json;
using System.Text.Json.Serialization;
using AguiaBranca.Application.Features.Reports;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Tests.Reports;

/// <summary>
/// Paridade com o dashboard do app (<c>DashboardComputer.kt</c>). Os resultados esperados dos arquivos em
/// <c>Golden/*.json</c> foram <b>calculados à mão</b> aplicando as regras do Kotlin (não gerados pelo código C#):
/// se um teste falhar, revise o cálculo manual antes de "acertar" o código.
/// </summary>
public class ReportCalculatorGoldenTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static TheoryData<string> Files => new()
    {
        "01-todos-os-periodos.json", "02-mes-atual-e-divisao.json", "03-trimestre-virada-de-ano.json"
    };

    [Theory]
    [MemberData(nameof(Files))]
    public void Compute_MatchesHandDerivedExpectation(string file)
    {
        var golden = JsonSerializer.Deserialize<GoldenCase>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Reports", "Golden", file)), Json)!;

        var guidelines = golden.Guidelines.ToDictionary(g => g.Key, g => ReportTestKit.Guideline(g.Title));
        string? Id(string? key) => key is null ? null : guidelines[key].Id;

        var ideas = golden.Ideas.Select(i => ReportTestKit.Idea(i.Division, i.Status, i.CreatedAt, Id(i.Guideline))).ToList();
        var projects = golden.Projects.Select(p => ReportTestKit.Project(
            p.Title, p.Stage, p.Division, Id(p.Guideline), p.Investment, p.FinancialReturn, p.ProductivityGain, p.CostReduction,
            p.TargetDate, p.UpdatedAt)).ToList();

        var actual = ReportCalculator.Compute(
            ideas, projects, guidelines.Values.ToList(), new ReportFilters(golden.Filters.Period, golden.Filters.Division),
            golden.NowUtc, ReportTestKit.SaoPaulo);

        actual.Funnel.Should().Be(golden.Expected.Funnel);
        actual.Kpis.Should().Be(golden.Expected.Kpis);
        actual.Sparkline.Should().Equal(golden.Expected.Sparkline);
        actual.GuidelineImpacts
            .Select(g => new ExpectedImpact(g.Title, g.IdeasCount, g.ProjectsCount, g.Investment, g.FinancialReturn, g.NetProfit, g.RoiPercent))
            .Should().Equal(golden.Expected.GuidelineImpacts);
        actual.Projects
            .Select(p => new ExpectedProject(p.Title, p.Stage, p.RoiPercent, p.DaysToDeadline, p.Overdue))
            .Should().Equal(golden.Expected.Projects);
    }

    private sealed record GoldenCase(
        string Description, DateTime NowUtc, ReportFilters Filters, List<GGuideline> Guidelines, List<GIdea> Ideas,
        List<GProject> Projects, GExpected Expected);

    private sealed record GGuideline(string Key, string Title);
    private sealed record GIdea(Division Division, string? Guideline, IdeaStatus Status, DateTime CreatedAt);
    private sealed record GProject(
        string Title, ProjectStage Stage, Division Division, string? Guideline, decimal Investment, decimal FinancialReturn,
        decimal ProductivityGain, decimal CostReduction, DateTime? TargetDate, DateTime UpdatedAt);
    private sealed record GExpected(
        FunnelReport Funnel, KpisReport Kpis, List<SparklinePoint> Sparkline, List<ExpectedImpact> GuidelineImpacts, List<ExpectedProject> Projects);
    private sealed record ExpectedImpact(
        string Title, int IdeasCount, int ProjectsCount, decimal Investment, decimal FinancialReturn, decimal NetProfit, decimal? RoiPercent);
    private sealed record ExpectedProject(string Title, ProjectStage Stage, decimal? RoiPercent, int? DaysToDeadline, bool Overdue);
}
