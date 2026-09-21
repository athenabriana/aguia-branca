using AguiaBranca.Application.Features.Reports;
using AguiaBranca.Domain.Enums;
using static AguiaBranca.Application.Tests.Reports.ReportTestKit;

namespace AguiaBranca.Application.Tests.Reports;

public class ReportCalculatorTests
{
    [Fact]
    public void EmptyData_GivesZerosNullRoiAndSixEmptyMonths()
    {
        var s = Compute();

        s.Funnel.Should().Be(new FunnelReport(0, 0, 0, 0, 0));
        s.Kpis.Should().Be(new KpisReport(null, 0, 0, 0, 0, 0, 0, 0));
        s.Sparkline.Should().HaveCount(6).And.OnlyContain(p => p.RoiPercent == null);
        s.Sparkline.Select(p => p.Month).Should().Equal("2026-04", "2026-05", "2026-06", "2026-07", "2026-08", "2026-09");
        s.GuidelineImpacts.Should().BeEmpty();
        s.Projects.Should().BeEmpty();
    }

    [Fact]
    public void ZeroInvestment_MakesRoiNullEverywhere_AndDoesNotDivideByZero()
    {
        var g = Guideline("Sem custo");
        var s = Compute(
            projects: [Project(guidelineId: g.Id, investment: 0m, financialReturn: 50_000m, updatedAt: Utc(2026, 9, 10))],
            guidelines: [g]);

        s.Kpis.RoiConsolidated.Should().BeNull();
        s.Kpis.NetProfit.Should().Be(50_000m);
        s.Sparkline[^1].RoiPercent.Should().BeNull();
        s.GuidelineImpacts.Single().RoiPercent.Should().BeNull();
        s.Projects.Single().RoiPercent.Should().BeNull();
    }

    [Fact]
    public void FewerThanSixMonthsOfData_StillReturnsSixPointsWithNullsForEmptyMonths()
    {
        var s = Compute(projects: [Project(investment: 100m, financialReturn: 150m, updatedAt: Utc(2026, 8, 20))]);

        s.Sparkline.Should().HaveCount(6);
        s.Sparkline.Where(p => p.RoiPercent != null).Should().ContainSingle().Which.Should().Be(new SparklinePoint("2026-08", 50m));
    }

    [Fact]
    public void Sparkline_IgnoresPeriod_ButRespectsDivision()
    {
        var old = Utc(2026, 5, 10);
        var projects = new[]
        {
            Project(division: Division.LOGISTICA, investment: 100m, financialReturn: 200m, updatedAt: old),
            Project(division: Division.COMERCIO, investment: 100m, financialReturn: 0m, updatedAt: old)
        };

        var byPeriod = Compute(projects: projects, period: Period.THIS_MONTH);
        var byDivision = Compute(projects: projects, division: Division.LOGISTICA);

        byPeriod.Sparkline.Single(p => p.Month == "2026-05").RoiPercent.Should().Be(0m, "os dois projetos entram (retorno 200 / investimento 200); THIS_MONTH não filtra a sparkline");
        byPeriod.Projects.Should().BeEmpty("mas filtra a lista");
        byDivision.Sparkline.Single(p => p.Month == "2026-05").RoiPercent.Should().Be(100m);
    }

    [Fact]
    public void CombinedFilters_ApplyDivisionAndPeriodToFunnelKpisAndLists()
    {
        var g = Guideline("Estratégia");
        var ideas = new[]
        {
            Idea(Division.LOGISTICA, IdeaStatus.APROVADA, Utc(2026, 9, 5), g.Id),
            Idea(Division.LOGISTICA, IdeaStatus.APROVADA, Utc(2026, 8, 5), g.Id),   // fora do mês
            Idea(Division.COMERCIO, IdeaStatus.APROVADA, Utc(2026, 9, 5), g.Id)     // outra divisão
        };
        var projects = new[]
        {
            Project("dentro", division: Division.LOGISTICA, guidelineId: g.Id, investment: 10m, financialReturn: 30m, updatedAt: Utc(2026, 9, 6)),
            Project("outro mês", division: Division.LOGISTICA, guidelineId: g.Id, investment: 10m, financialReturn: 30m, updatedAt: Utc(2026, 8, 6)),
            Project("outra divisão", division: Division.COMERCIO, guidelineId: g.Id, investment: 10m, financialReturn: 30m, updatedAt: Utc(2026, 9, 6))
        };

        var s = Compute(ideas, projects, [g], Period.THIS_MONTH, Division.LOGISTICA);

        s.Funnel.Submitted.Should().Be(1);
        s.Projects.Select(p => p.Title).Should().Equal("dentro");
        s.Kpis.TotalInvestment.Should().Be(10m);
        s.GuidelineImpacts.Single().Should().Match<GuidelineImpactReport>(i => i.IdeasCount == 1 && i.ProjectsCount == 1);
    }

    [Fact]
    public void ThisYear_UsesLocalYearStart_EvenWhenUtcAlreadyRolledOver()
    {
        // 2026-01-01T02:00Z = 31/12/2025 23:00 em São Paulo → "este ano" ainda é 2025.
        var now = new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc);
        var s = Compute(
            projects:
            [
                Project("em 2025", investment: 10m, updatedAt: Utc(2025, 2, 1)),
                Project("em 2024", investment: 10m, updatedAt: Utc(2024, 12, 31, 12))
            ],
            period: Period.THIS_YEAR, now: now);

        s.Projects.Select(p => p.Title).Should().Equal("em 2025");
    }

    [Fact]
    public void LastQuarter_IsARollingThreeMonthWindow_WithDayClampedLikeTheApp()
    {
        // 31/05 − 3 meses = 28/02 (2026 não é bissexto): início 2026-02-28 00:00 local = 03:00Z.
        var now = new DateTime(2026, 5, 31, 15, 0, 0, DateTimeKind.Utc);
        var s = Compute(
            projects:
            [
                Project("no limite", updatedAt: new DateTime(2026, 2, 28, 3, 0, 0, DateTimeKind.Utc)),
                Project("um segundo antes", updatedAt: new DateTime(2026, 2, 28, 2, 59, 59, DateTimeKind.Utc))
            ],
            period: Period.LAST_QUARTER, now: now);

        s.Projects.Select(p => p.Title).Should().Equal("no limite");
    }

    [Fact]
    public void ThisMonth_IncludesTheExactWindowEnd_AndExcludesTheFuture()
    {
        var s = Compute(
            projects:
            [
                Project("agora", updatedAt: Now),
                Project("futuro", updatedAt: Now.AddSeconds(1))
            ],
            period: Period.THIS_MONTH);

        s.Projects.Select(p => p.Title).Should().Equal("agora");
    }

    [Fact]
    public void OverdueProjects_IgnoreFinishedOnes_AndTheDeadlineDayItself()
    {
        var yesterday = Utc(2026, 9, 20, 0);
        var today = Utc(2026, 9, 21, 0);
        var s = Compute(projects:
        [
            Project("atrasado", ProjectStage.EM_EXECUCAO, targetDate: yesterday),
            Project("planejando atrasado", ProjectStage.PLANEJAMENTO, targetDate: yesterday),
            Project("vence hoje", ProjectStage.EM_EXECUCAO, targetDate: today),
            Project("concluído", ProjectStage.CONCLUIDO, targetDate: yesterday),
            Project("cancelado", ProjectStage.CANCELADO, targetDate: yesterday)
        ]);

        s.Kpis.OverdueProjects.Should().Be(2);
        s.Projects.Where(p => p.Overdue).Select(p => p.Title).Should().BeEquivalentTo("atrasado", "planejando atrasado");
        s.Projects.Single(p => p.Title == "concluído").DaysToDeadline.Should().BeNull();
    }

    [Fact]
    public void Today_FollowsTheReportTimeZone_NotUtc()
    {
        // 2026-09-22T01:00Z = 21/09 22:00 em São Paulo: um prazo em 21/09 ainda não venceu.
        var now = new DateTime(2026, 9, 22, 1, 0, 0, DateTimeKind.Utc);
        var s = Compute(projects: [Project(targetDate: Utc(2026, 9, 21, 0), updatedAt: now)], now: now);

        s.Projects.Single().DaysToDeadline.Should().Be(0);
        s.Projects.Single().Overdue.Should().BeFalse();
    }

    [Fact]
    public void GuidelineImpacts_ListProjectsFirst_ByRoiDescending_AndThenTheRest()
    {
        var low = Guideline("Baixo");
        var high = Guideline("Alto");
        var none = Guideline("Sem projeto");
        var s = Compute(
            projects:
            [
                Project(guidelineId: low.Id, investment: 100m, financialReturn: 50m),
                Project(guidelineId: high.Id, investment: 100m, financialReturn: 300m)
            ],
            guidelines: [none, low, high]);

        s.GuidelineImpacts.Select(g => g.Title).Should().Equal("Alto", "Baixo", "Sem projeto");
    }

    [Fact]
    public void EqualRoi_IsOrderedByTitle_SoTheResponseIsDeterministic()
    {
        var s = Compute(projects:
        [
            Project("Zeta", investment: 10m, financialReturn: 20m),
            Project("Alfa", investment: 10m, financialReturn: 20m),
            Project("Meio", investment: 10m, financialReturn: 20m)
        ]);

        s.Projects.Select(p => p.Title).Should().Equal("Alfa", "Meio", "Zeta");
    }

    [Theory]
    [InlineData(3, 4, 33.33)]           // 33,333…
    [InlineData(200, 201.01, 0.51)]     // 0,505 → meio arredonda para longe do zero
    [InlineData(8, 9, 12.5)]
    [InlineData(300, 100, -66.67)]      // −66,666…
    public void Roi_IsRoundedToTwoDecimalsAwayFromZero(double investment, double financialReturn, double expected)
    {
        var s = Compute(projects: [Project(investment: (decimal)investment, financialReturn: (decimal)financialReturn)]);

        s.Kpis.RoiConsolidated.Should().Be((decimal)expected);
        s.Projects.Single().RoiPercent.Should().Be((decimal)expected);
    }

    [Fact]
    public void AverageProductivityGain_OnlyCountsProjectsWithPositiveGain()
    {
        var s = Compute(projects:
        [
            Project(productivityGain: 30m), Project(productivityGain: 10m), Project(productivityGain: 0m), Project(productivityGain: 0m)
        ]);

        s.Kpis.AvgProductivityGain.Should().Be(20m);
    }

    [Fact]
    public void GuidelineDetail_AggregatesIdeasAndProjectsOfThatGuidelineOnly()
    {
        var mine = Guideline("Minha");
        var other = Guideline("Outra");
        var ideas = new[]
        {
            Idea(Division.LOGISTICA, IdeaStatus.APROVADA, Utc(2026, 9, 1), mine.Id, "Ideia A"),
            Idea(Division.LOGISTICA, IdeaStatus.SUBMETIDA, Utc(2026, 9, 2), mine.Id, "Ideia B"),
            Idea(Division.LOGISTICA, IdeaStatus.APROVADA, Utc(2026, 9, 3), other.Id, "Ideia C")
        };
        var projects = new[]
        {
            Project("P1", guidelineId: mine.Id, investment: 100m, financialReturn: 175m),
            Project("P2", guidelineId: mine.Id, investment: 100m, financialReturn: 25m),
            Project("P3", guidelineId: other.Id, investment: 999m, financialReturn: 999m)
        };

        var d = ReportCalculator.ComputeGuideline(mine, ideas, projects, new ReportFilters(), Now, SaoPaulo);

        d.IdeasCount.Should().Be(2);
        d.IdeasByStatus.Single(x => x.Status == IdeaStatus.APROVADA).Count.Should().Be(1);
        d.IdeasByStatus.Should().HaveCount(Enum.GetValues<IdeaStatus>().Length);
        d.Ideas.Select(i => i.Title).Should().Equal("Ideia B", "Ideia A"); // mais recente primeiro
        d.ProjectsCount.Should().Be(2);
        d.Projects.Select(p => p.Title).Should().Equal("P1", "P2");
        (d.Investment, d.FinancialReturn, d.NetProfit, d.RoiPercent).Should().Be((200m, 200m, 0m, 0m));
    }

    [Fact]
    public void ProjectReport_ExposesDeadlineAndGuidelineTitle()
    {
        var p = Project("Meta", ProjectStage.EM_EXECUCAO, investment: 50m, financialReturn: 75m, productivityGain: 12m, costReduction: 8m,
            targetDate: Utc(2026, 9, 30, 0));

        var r = ReportCalculator.ToProjectReport(p, "Orientação X", Now, SaoPaulo);

        r.GuidelineTitle.Should().Be("Orientação X");
        r.DaysToDeadline.Should().Be(9);
        r.Overdue.Should().BeFalse();
        (r.NetProfit, r.RoiPercent, r.ProductivityGain, r.CostReduction).Should().Be((25m, 50m, 12m, 8m));
    }
}
