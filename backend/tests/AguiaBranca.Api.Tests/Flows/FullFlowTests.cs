using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;
using static AguiaBranca.Api.Tests.Support.ApiTestKit;

namespace AguiaBranca.Api.Tests.Flows;

/// <summary>
/// B22 — jornada completa contra API + MongoDB reais, na ordem do negócio: orientação (líder) → ideia (operador, +15) →
/// ICE (gestor) → aprovação (projeto rascunho, +50) → edição do projeto (histórico com diff) → conclusão (ideia
/// IMPLEMENTADA, +200, "Impacto Real") → dashboard do líder → ranking mensal → histórico da orientação.
/// Os números do dashboard são conferidos por <b>variação</b> (o banco também tem os dados do seed).
/// </summary>
[Trait("Category", "Integration")]
public sealed class FullFlowTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await r.Content.ReadAsStringAsync();
        r.StatusCode.Should().Be(expected, text);
        return JsonDocument.Parse(text).RootElement;
    }

    private static decimal Roi(JsonElement e) => e.GetProperty("roiPercent").GetDecimal();

    [Fact]
    public async Task IdeaJourney_FromGuidelineToDashboard_KeepsPointsBadgesAndReportsConsistent()
    {
        using var lider = api.ClientAs(Role.LIDER);
        using var gestor = api.ClientAs(Role.GESTOR);
        using var operador = await NewUserClientAsync(api, name: "Operadora Jornada");

        var before = await Ok(await lider.GetAsync("/api/v1/reports/summary"));

        // 1) Líder cria a orientação estratégica.
        var guideline = await Ok(await lider.PostAsJsonAsync("/api/v1/guidelines", new
        {
            title = $"Jornada completa {Unique()}", description = "Orientação criada pelo fluxo E2E.", pillar = "PROJETOS", campaign = "Campanha E2E"
        }), HttpStatusCode.Created);
        var guidelineId = guideline.GetProperty("id").GetString()!;

        // 2) Operadora cria a ideia vinculada: +10 +5.
        var idea = await CreateIdeaAsync(operador, guidelineId, "Ideia da jornada completa");
        var ideaId = idea.GetProperty("id").GetString()!;
        idea.GetProperty("pointsAwarded").GetInt32().Should().Be(15);
        (await Points(operador)).Should().Be(15);
        (await Badges(operador)).Should().BeEquivalentTo(["Primeira Ideia"], "'Estrategista' só vem com ideia vinculada já aprovada");

        // 3) Gestor avalia com ICE: SUBMETIDA → EM_ANALISE.
        var iced = await Ok(await gestor.PutAsJsonAsync($"/api/v1/ideas/{ideaId}/ice", new { impact = 9, confidence = 9, ease = 8 }));
        iced.GetProperty("status").GetString().Should().Be("EM_ANALISE");
        iced.GetProperty("ice").GetProperty("score").GetInt32().Should().Be(648);

        // 4) Gestor aprova: projeto rascunho + 50 ao autor.
        var approved = await Ok(await gestor.PostAsync($"/api/v1/ideas/{ideaId}/approve", null));
        var projectId = approved.GetProperty("projectId").GetString()!;
        (await Points(operador)).Should().Be(65);
        (await GetIdeaAsync(operador, ideaId)).GetProperty("status").GetString().Should().Be("APROVADA");
        (await Badges(operador)).Should().BeEquivalentTo("Primeira Ideia", "Estrategista");

        var draft = await Ok(await gestor.GetAsync($"/api/v1/projects/{projectId}"));
        draft.GetProperty("stage").GetString().Should().Be("PLANEJAMENTO");
        draft.GetProperty("originatingIdeaId").GetString().Should().Be(ideaId);
        draft.GetProperty("guidelineId").GetString().Should().Be(guidelineId);
        draft.GetProperty("priorityScore").GetInt32().Should().Be(648);

        // 5) Gestor põe o projeto em execução e informa o investimento: o histórico guarda o diff.
        object Body(string stage, decimal financialReturn, string note) => new
        {
            title = draft.GetProperty("title").GetString(), description = "Escopo detalhado", stage, statusText = "Em andamento",
            investment = 100000, targetDate = DateTime.UtcNow.Date.AddDays(60).ToString("yyyy-MM-dd'T'00:00:00'Z'"), financialReturn,
            productivityGain = 12.5, costReduction = 40000, division = "LOGISTICA", guidelineId, note
        };
        (await Ok(await gestor.PutAsJsonAsync($"/api/v1/projects/{projectId}", Body("EM_EXECUCAO", 0, "Kick-off"))))
            .GetProperty("version").GetInt32().Should().Be(2);
        (await Points(operador)).Should().Be(65, "editar o projeto não concede pontos");

        var timeline = (await Ok(await gestor.GetAsync($"/api/v1/projects/{projectId}/updates"))).GetProperty("items").EnumerateArray().ToList();
        timeline.Should().NotBeEmpty();
        timeline[0].GetProperty("note").GetString().Should().Be("Kick-off");
        timeline[0].GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("field").GetString())
            .Should().Contain(["stage", "investment"]);
        timeline[0].GetProperty("changes").EnumerateArray().Single(c => c.GetProperty("field").GetString() == "investment")
            .GetProperty("to").GetDecimal().Should().Be(100000m);

        // 6) Dashboard já vê o projeto em execução.
        var midway = await Ok(await lider.GetAsync("/api/v1/reports/summary"));
        Delta(midway, before, "kpis", "activeProjects").Should().Be(1);
        Delta(midway, before, "kpis", "totalInvestment").Should().Be(100000m);

        // 7) Conclusão: ideia IMPLEMENTADA, +200 e badge "Impacto Real".
        (await Ok(await gestor.PutAsJsonAsync($"/api/v1/projects/{projectId}", Body("CONCLUIDO", 300000, "Meta atingida"))))
            .GetProperty("stage").GetString().Should().Be("CONCLUIDO");
        (await GetIdeaAsync(operador, ideaId)).GetProperty("status").GetString().Should().Be("IMPLEMENTADA");
        (await Points(operador)).Should().Be(265);
        (await Badges(operador)).Should().Contain("Impacto Real");

        // 8) O líder lê o dashboard com os números esperados (variação exata sobre o seed).
        var after = await Ok(await lider.GetAsync("/api/v1/reports/summary"));
        Delta(after, before, "funnel", "submitted").Should().Be(1);
        Delta(after, before, "funnel", "evaluated").Should().Be(1);
        Delta(after, before, "funnel", "approved").Should().Be(1);
        Delta(after, before, "funnel", "inExecution").Should().Be(1);
        Delta(after, before, "funnel", "roiPositive").Should().Be(1);
        Delta(after, before, "kpis", "totalInvestment").Should().Be(100000m);
        Delta(after, before, "kpis", "totalReturn").Should().Be(300000m);
        Delta(after, before, "kpis", "netProfit").Should().Be(200000m);
        Delta(after, before, "kpis", "totalCostReduction").Should().Be(40000m);
        Delta(after, before, "kpis", "activeProjects").Should().Be(0, "o projeto deixou de estar em execução");

        var impact = after.GetProperty("guidelineImpacts").EnumerateArray().Single(g => g.GetProperty("guidelineId").GetString() == guidelineId);
        (impact.GetProperty("ideasCount").GetInt32(), impact.GetProperty("projectsCount").GetInt32()).Should().Be((1, 1));
        Roi(impact).Should().Be(200m);
        after.GetProperty("projects").EnumerateArray().Single(p => p.GetProperty("id").GetString() == projectId)
            .GetProperty("overdue").GetBoolean().Should().BeFalse();

        var guidelineReport = await Ok(await lider.GetAsync($"/api/v1/reports/guidelines/{guidelineId}"));
        (guidelineReport.GetProperty("investment").GetDecimal(), guidelineReport.GetProperty("financialReturn").GetDecimal(),
            guidelineReport.GetProperty("netProfit").GetDecimal()).Should().Be((100000m, 300000m, 200000m));
        guidelineReport.GetProperty("ideasByStatus").EnumerateArray().Single(s => s.GetProperty("status").GetString() == "IMPLEMENTADA")
            .GetProperty("count").GetInt32().Should().Be(1);

        var projectReport = await Ok(await lider.GetAsync($"/api/v1/reports/projects/{projectId}"));
        Roi(projectReport).Should().Be(200m);
        projectReport.GetProperty("daysToDeadline").ValueKind.Should().Be(JsonValueKind.Null, "projeto concluído não tem prazo pendente");

        // 9) Ranking mensal reflete os pontos do mês.
        var ranking = (await Ok(await operador.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray().ToList();
        ranking.Single(r => r.GetProperty("name").GetString() == "Operadora Jornada").GetProperty("monthPoints").GetInt32().Should().Be(265);
        ranking.Select(r => r.GetProperty("monthPoints").GetInt32()).Should().BeInDescendingOrder();

        // 10) Histórico da orientação: criação + edição.
        (await Ok(await lider.PutAsJsonAsync($"/api/v1/guidelines/{guidelineId}", new
        {
            title = guideline.GetProperty("title").GetString(), description = "Descrição revisada", pillar = "MENSURACAO", campaign = "Campanha E2E 2"
        }))).GetProperty("pillar").GetString().Should().Be("MENSURACAO");
        var history = (await Ok(await lider.GetAsync($"/api/v1/guidelines/history?guidelineId={guidelineId}"))).GetProperty("items").EnumerateArray().ToList();
        history.Select(h => h.GetProperty("action").GetString()).Should().Equal("UPDATED", "CREATED");
        history[0].GetProperty("category").GetString().Should().Be("MENSURACAO");
        history[1].GetProperty("category").GetString().Should().Be("PROJETOS");

        // 11) Perfil final: pontos e badges persistidos no servidor.
        var me = await Me(operador);
        me.GetProperty("points").GetInt32().Should().Be(265);
        me.GetProperty("badges").EnumerateArray().Select(b => b.GetString()).Should().Contain(["Primeira Ideia", "Estrategista", "Impacto Real"]);

        // 12) Operador e gestor continuam sem acesso ao dashboard.
        (await operador.GetAsync("/api/v1/reports/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await gestor.GetAsync("/api/v1/reports/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static decimal Delta(JsonElement after, JsonElement before, string section, string field) =>
        after.GetProperty(section).GetProperty(field).GetDecimal() - before.GetProperty(section).GetProperty(field).GetDecimal();
}
