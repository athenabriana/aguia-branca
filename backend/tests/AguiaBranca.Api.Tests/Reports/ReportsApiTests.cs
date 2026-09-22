using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;
using static AguiaBranca.Api.Tests.Support.ApiTestKit;

namespace AguiaBranca.Api.Tests.Reports;

[Trait("Category", "Integration")]
public sealed class ReportsApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string Base = "/api/v1/reports";
    private const string Projects = "/api/v1/projects";

    private async Task<JsonElement> SummaryAsync(string query = "")
    {
        using var lider = api.ClientAs(Role.LIDER);
        var response = await lider.GetAsync($"{Base}/summary{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    private async Task<string> CreateProjectAsync(
        string? title = null, string stage = "EM_EXECUCAO", decimal investment = 1000, decimal financialReturn = 0,
        string division = "LOGISTICA", string? targetDate = null, string? guidelineId = null)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var response = await gestor.PostAsJsonAsync(Projects, new
        {
            title = title ?? $"Projeto relatório {Unique()}", description = "Descrição", stage, statusText = "Status", investment,
            targetDate, financialReturn, productivityGain = 0, costReduction = 0, division, guidelineId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private async Task<List<JsonElement>> AllProjectsAsync()
    {
        using var lider = api.ClientAs(Role.LIDER);
        return (await Json(await lider.GetAsync($"{Projects}?pageSize=200"))).GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Summary_AsLider_ReturnsTheDashboardContract()
    {
        var s = await SummaryAsync();

        s.GetProperty("period").GetString().Should().Be("ALL");
        s.GetProperty("division").ValueKind.Should().Be(JsonValueKind.Null);
        s.GetProperty("funnel").EnumerateObject().Select(p => p.Name)
            .Should().Equal("submitted", "evaluated", "approved", "inExecution", "roiPositive");
        s.GetProperty("kpis").EnumerateObject().Select(p => p.Name).Should().Equal(
            "roiConsolidated", "netProfit", "totalInvestment", "totalReturn", "activeProjects", "avgProductivityGain",
            "totalCostReduction", "overdueProjects");
        s.GetProperty("guidelineImpacts").GetArrayLength().Should().BeGreaterThan(0);
        s.GetProperty("projects").GetArrayLength().Should().BeGreaterThan(0);

        var sparkline = s.GetProperty("sparkline").EnumerateArray().ToList();
        sparkline.Should().HaveCount(6);
        sparkline.Select(p => p.GetProperty("month").GetString()).Should().BeInAscendingOrder(StringComparer.Ordinal);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        sparkline[^1].GetProperty("month").GetString().Should().Be($"{local:yyyy-MM}");
    }

    [Fact]
    public async Task Summary_TotalsMatchTheProjectsEndpoint()
    {
        var s = await SummaryAsync();
        var projects = await AllProjectsAsync();

        s.GetProperty("funnel").GetProperty("submitted").GetInt32().Should().BeGreaterThan(0);
        s.GetProperty("projects").GetArrayLength().Should().Be(projects.Count);
        s.GetProperty("kpis").GetProperty("totalInvestment").GetDecimal().Should().Be(projects.Sum(p => p.GetProperty("investment").GetDecimal()));
        s.GetProperty("kpis").GetProperty("totalReturn").GetDecimal().Should().Be(projects.Sum(p => p.GetProperty("financialReturn").GetDecimal()));
        s.GetProperty("kpis").GetProperty("activeProjects").GetInt32().Should().Be(projects.Count(p => p.GetProperty("stage").GetString() == "EM_EXECUCAO"));
        s.GetProperty("funnel").GetProperty("inExecution").GetInt32()
            .Should().Be(projects.Count(p => p.GetProperty("stage").GetString() is "EM_EXECUCAO" or "CONCLUIDO"));

        var byRoi = s.GetProperty("projects").EnumerateArray()
            .Select(p => p.GetProperty("roiPercent") is { ValueKind: JsonValueKind.Number } roi ? roi.GetDecimal() : decimal.MinValue).ToList();
        byRoi.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Summary_DivisionFilter_ReducesFunnelKpisImpactAndProjects()
    {
        var all = await SummaryAsync();
        var scoped = await SummaryAsync("?division=LOGISTICA");

        scoped.GetProperty("division").GetString().Should().Be("LOGISTICA");
        scoped.GetProperty("projects").EnumerateArray().Should().OnlyContain(p => p.GetProperty("division").GetString() == "LOGISTICA");
        scoped.GetProperty("projects").GetArrayLength().Should().BeLessThan(all.GetProperty("projects").GetArrayLength());
        scoped.GetProperty("funnel").GetProperty("submitted").GetInt32().Should().BeLessThan(all.GetProperty("funnel").GetProperty("submitted").GetInt32());
        scoped.GetProperty("kpis").GetProperty("totalInvestment").GetDecimal()
            .Should().BeLessThan(all.GetProperty("kpis").GetProperty("totalInvestment").GetDecimal());
        scoped.GetProperty("guidelineImpacts").GetArrayLength().Should().Be(all.GetProperty("guidelineImpacts").GetArrayLength(),
            "todas as orientações aparecem, mesmo sem projetos na divisão");
    }

    [Theory]
    [InlineData("THIS_MONTH")]
    [InlineData("LAST_QUARTER")]
    [InlineData("THIS_YEAR")]
    [InlineData("ALL")]
    public async Task Summary_EveryPeriod_IncludesAProjectEditedJustNow(string period)
    {
        var id = await CreateProjectAsync();

        var s = await SummaryAsync($"?period={period}");

        s.GetProperty("period").GetString().Should().Be(period);
        s.GetProperty("projects").EnumerateArray().Should().Contain(p => p.GetProperty("id").GetString() == id);
    }

    [Theory]
    [InlineData("?period=NADA")]
    [InlineData("?division=MARTE")]
    public async Task Summary_InvalidFilters_Return400(string query)
    {
        using var lider = api.ClientAs(Role.LIDER);
        var response = await lider.GetAsync($"{Base}/summary{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Theory]
    [InlineData(Role.GESTOR, "summary")]
    [InlineData(Role.OPERADOR, "summary")]
    [InlineData(Role.GESTOR, "guidelines")]
    [InlineData(Role.OPERADOR, "guidelines")]
    [InlineData(Role.GESTOR, "guidelines/665f00000000000000000001")]
    [InlineData(Role.OPERADOR, "projects/665f00000000000000000001")]
    public async Task NonLeaders_GetForbidden(Role role, string path)
    {
        using var client = api.ClientAs(role);
        (await client.GetAsync($"{Base}/{path}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("guidelines")]
    [InlineData("guidelines/665f00000000000000000001")]
    [InlineData("projects/665f00000000000000000001")]
    public async Task WithoutToken_Returns401(string path) =>
        (await api.Anonymous.GetAsync($"{Base}/{path}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task EditingAProjectsFinancialReturn_ChangesTheKpisOnTheNextCall()
    {
        var id = await CreateProjectAsync(investment: 4000, financialReturn: 1000);
        var before = (await SummaryAsync()).GetProperty("kpis");

        using var gestor = api.ClientAs(Role.GESTOR);
        var current = await Json(await gestor.GetAsync($"{Projects}/{id}"));
        var put = await gestor.PutAsJsonAsync($"{Projects}/{id}", new
        {
            title = current.GetProperty("title").GetString(), description = "Descrição", stage = "EM_EXECUCAO", statusText = "Status",
            investment = 4000, targetDate = (string?)null, financialReturn = 3500, productivityGain = 0, costReduction = 0,
            division = "LOGISTICA", guidelineId = (string?)null
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var after = (await SummaryAsync()).GetProperty("kpis");
        (after.GetProperty("totalReturn").GetDecimal() - before.GetProperty("totalReturn").GetDecimal()).Should().Be(2500m);
        (after.GetProperty("netProfit").GetDecimal() - before.GetProperty("netProfit").GetDecimal()).Should().Be(2500m);
        after.GetProperty("totalInvestment").GetDecimal().Should().Be(before.GetProperty("totalInvestment").GetDecimal());

        var project = await Json(await api.ClientAs(Role.LIDER).GetAsync($"{Base}/projects/{id}"));
        project.GetProperty("financialReturn").GetDecimal().Should().Be(3500m);
        project.GetProperty("roiPercent").GetDecimal().Should().Be(-12.5m);
    }

    [Fact]
    public async Task OverdueProjects_AreFlaggedAndCounted()
    {
        var past = DateTime.UtcNow.Date.AddDays(-10).ToString("yyyy-MM-dd'T'00:00:00'Z'");
        var id = await CreateProjectAsync(targetDate: past);

        var s = await SummaryAsync();
        var list = s.GetProperty("projects").EnumerateArray().ToList();
        var mine = list.Single(p => p.GetProperty("id").GetString() == id);

        mine.GetProperty("overdue").GetBoolean().Should().BeTrue();
        mine.GetProperty("daysToDeadline").GetInt32().Should().BeLessThan(0);
        s.GetProperty("kpis").GetProperty("overdueProjects").GetInt32().Should().Be(list.Count(p => p.GetProperty("overdue").GetBoolean()));
        list.Where(p => p.GetProperty("overdue").GetBoolean())
            .Should().OnlyContain(p => p.GetProperty("stage").GetString() != "CONCLUIDO" && p.GetProperty("stage").GetString() != "CANCELADO");
    }

    [Fact]
    public async Task Guidelines_ListsTheImpactOfEachOne_ProjectsFirstThenByRoi()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var response = await lider.GetAsync($"{Base}/guidelines");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await Json(response)).GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Select(i => i.GetProperty("projectsCount").GetInt32() > 0).Should().BeInDescendingOrder("orientações com projeto vêm primeiro");
        var summaryImpacts = (await SummaryAsync()).GetProperty("guidelineImpacts").EnumerateArray().Select(i => i.GetProperty("guidelineId").GetString());
        items.Select(i => i.GetProperty("guidelineId").GetString()).Should().Equal(summaryImpacts);
    }

    [Fact]
    public async Task GuidelineDetail_ReturnsIdeasProjectsAndFinancials()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var items = (await Json(await lider.GetAsync($"{Base}/guidelines"))).GetProperty("items").EnumerateArray().ToList();
        var withProjects = items.First(i => i.GetProperty("projectsCount").GetInt32() > 0);
        var id = withProjects.GetProperty("guidelineId").GetString()!;

        var response = await lider.GetAsync($"{Base}/guidelines/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var d = await Json(response);
        d.GetProperty("id").GetString().Should().Be(id);
        d.GetProperty("title").GetString().Should().Be(withProjects.GetProperty("title").GetString());
        d.GetProperty("projectsCount").GetInt32().Should().Be(withProjects.GetProperty("projectsCount").GetInt32());
        d.GetProperty("ideasCount").GetInt32().Should().Be(withProjects.GetProperty("ideasCount").GetInt32());
        d.GetProperty("ideas").GetArrayLength().Should().Be(d.GetProperty("ideasCount").GetInt32());
        d.GetProperty("ideasByStatus").EnumerateArray().Sum(s => s.GetProperty("count").GetInt32()).Should().Be(d.GetProperty("ideasCount").GetInt32());
        d.GetProperty("projects").EnumerateArray().Sum(p => p.GetProperty("investment").GetDecimal()).Should().Be(d.GetProperty("investment").GetDecimal());
        d.GetProperty("netProfit").GetDecimal().Should().Be(d.GetProperty("financialReturn").GetDecimal() - d.GetProperty("investment").GetDecimal());
        d.GetProperty("roiPercent").GetDecimal().Should().Be(withProjects.GetProperty("roiPercent").GetDecimal());
    }

    [Theory]
    [InlineData("guidelines/665f0000000000000000abcd")]
    [InlineData("projects/665f0000000000000000abcd")]
    [InlineData("guidelines/nao-e-um-id")]
    [InlineData("projects/nao-e-um-id")]
    public async Task UnknownOrMalformedIds_Return404(string path)
    {
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.GetAsync($"{Base}/{path}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProjectReport_OfTheSeededCompletedProject_HasExactFinancials()
    {
        var seeded = (await AllProjectsAsync()).Single(p => p.GetProperty("title").GetString() == "PROJ: Painel de indicadores de pontualidade");
        using var lider = api.ClientAs(Role.LIDER);

        var r = await Json(await lider.GetAsync($"{Base}/projects/{seeded.GetProperty("id").GetString()}"));

        r.GetProperty("investment").GetDecimal().Should().Be(120000m);
        r.GetProperty("financialReturn").GetDecimal().Should().Be(310000m);
        r.GetProperty("netProfit").GetDecimal().Should().Be(190000m);
        r.GetProperty("roiPercent").GetDecimal().Should().Be(158.33m);
        r.GetProperty("stage").GetString().Should().Be("CONCLUIDO");
        r.GetProperty("daysToDeadline").ValueKind.Should().Be(JsonValueKind.Null);
        r.GetProperty("overdue").GetBoolean().Should().BeFalse();
        r.EnumerateObject().Select(p => p.Name).Should().Contain(
            ["productivityGain", "costReduction", "targetDate", "guidelineTitle", "statusText"]);
    }

    [Fact]
    public async Task ProjectReport_WithFutureDeadline_CountsCalendarDays()
    {
        var target = DateTime.UtcNow.Date.AddDays(30).ToString("yyyy-MM-dd'T'00:00:00'Z'");
        var id = await CreateProjectAsync(targetDate: target);
        using var lider = api.ClientAs(Role.LIDER);

        var r = await Json(await lider.GetAsync($"{Base}/projects/{id}"));

        r.GetProperty("daysToDeadline").GetInt32().Should().BeInRange(29, 31); // depende do dia local no fuso do relatório
        r.GetProperty("overdue").GetBoolean().Should().BeFalse();
    }
}
