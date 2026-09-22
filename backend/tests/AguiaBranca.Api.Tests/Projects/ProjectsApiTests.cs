using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using static AguiaBranca.Api.Tests.Support.ApiTestKit;

namespace AguiaBranca.Api.Tests.Projects;

[Trait("Category", "Integration")]
public sealed class ProjectsApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string Base = "/api/v1/projects";

    private static StringContent Raw(string json) => new(json, Encoding.UTF8, "application/json");

    private static object Body(
        string? title = null, string stage = "PLANEJAMENTO", decimal investment = 0, decimal financialReturn = 0,
        decimal productivityGain = 0, decimal costReduction = 0, string? guidelineId = null, string? responsibleId = null,
        string? targetDate = null, string? note = null, int? version = null, string division = "LOGISTICA", string statusText = "Em andamento") =>
        new
        {
            title = title ?? $"Projeto {Unique()}", description = "Descrição", stage, statusText, investment, targetDate,
            financialReturn, productivityGain, costReduction, division, guidelineId, responsibleId, note, version
        };

    private async Task<JsonElement> CreateAsync(HttpClient gestor, object? body = null)
    {
        var response = await gestor.PostAsJsonAsync(Base, body ?? Body());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    /// <summary>Projeto do seed pelo título (o banco é compartilhado: outros testes também criam/concluem projetos).</summary>
    private async Task<JsonElement> SeededProjectAsync(HttpClient client, string title) =>
        (await Json(await client.GetAsync($"{Base}?pageSize=200"))).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("title").GetString() == title);

    private async Task<List<JsonElement>> UpdatesOf(HttpClient client, string id) =>
        (await Json(await client.GetAsync($"{Base}/{id}/updates?pageSize=200"))).GetProperty("items").EnumerateArray().ToList();

    // =============================== PERMISSÕES ===============================

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(Role.OPERADOR, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(Role.LIDER, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(Role.GESTOR, HttpStatusCode.OK, HttpStatusCode.OK)]
    public async Task PermissionMatrix_ReadVsWrite(Role? role, HttpStatusCode read, HttpStatusCode write)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateAsync(gestor)).GetProperty("id").GetString();
        using var client = api.ClientAs(role);

        (await client.GetAsync(Base)).StatusCode.Should().Be(read);
        (await client.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(read);
        (await client.GetAsync($"{Base}/{id}/updates")).StatusCode.Should().Be(read);

        var put = await client.PutAsJsonAsync($"{Base}/{id}", Body(title: "Editado pelo teste"));
        put.StatusCode.Should().Be(write);
        var post = await client.PostAsJsonAsync(Base, Body());
        post.StatusCode.Should().Be(write == HttpStatusCode.OK ? HttpStatusCode.Created : write);
        if (write != HttpStatusCode.OK)
            (await client.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(write);
    }

    [Fact]
    public async Task TheLeaderCanReadButNeverChange_AnyProject()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var seeded = (await Json(await lider.GetAsync($"{Base}?pageSize=1"))).GetProperty("items")[0];
        var id = seeded.GetProperty("id").GetString();

        var put = await lider.PutAsJsonAsync($"{Base}/{id}", Body(title: "Tentativa do líder"));

        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(put)).GetProperty("code").GetString().Should().Be("FORBIDDEN");
        (await Json(await lider.GetAsync($"{Base}/{id}"))).GetProperty("title").GetString().Should().Be(seeded.GetProperty("title").GetString());
    }

    // =============================== CRIAR ===============================

    [Fact]
    public async Task Gestor_CreatesADirectProject_WithDefaults_AndTheCreationHistory()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var gestorId = (await Me(gestor)).GetProperty("id").GetString();

        var response = await gestor.PostAsync(Base, Raw("""
            {"title":"Modernização do check-in","description":"d","division":"PASSAGEIROS",
             "creatorManagerId":"665f00000000000000000999","originatingIdeaId":"665f00000000000000000998",
             "priorityScore":999,"version":77,"reporterId":"665f00000000000000000997","createdAt":"2001-01-01T00:00:00Z"}
            """));
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().Be($"{Base}/{body.GetProperty("id").GetString()}");
        body.GetProperty("stage").GetString().Should().Be("PLANEJAMENTO");
        body.GetProperty("creatorManagerId").GetString().Should().Be(gestorId, "vem do token, não do corpo");
        body.GetProperty("responsibleId").GetString().Should().Be(gestorId);
        body.GetProperty("originatingIdeaId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("priorityScore").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("reporterId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("roiPercent").ValueKind.Should().Be(JsonValueKind.Null, "investimento 0 → sem ROI");

        var history = await UpdatesOf(gestor, body.GetProperty("id").GetString()!);
        history.Should().ContainSingle();
        history[0].GetProperty("note").GetString().Should().Be("Projeto criado");
        history[0].GetProperty("authorId").GetString().Should().Be(gestorId);
        history[0].GetProperty("changes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Create_WithGuidelineAndResponsible_ResolvesTitleAndName()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var guidelineId = await AnyGuidelineIdAsync(api);
        var lider = (await Json(await gestor.GetAsync("/api/v1/users?role=LIDER"))).EnumerateArray().First();

        var body = await CreateAsync(gestor, Body(guidelineId: guidelineId, responsibleId: lider.GetProperty("id").GetString(),
            investment: 100000, financialReturn: 250000, targetDate: "2026-12-01"));

        body.GetProperty("guidelineTitle").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("responsibleName").GetString().Should().Be(lider.GetProperty("name").GetString());
        body.GetProperty("netProfit").GetDecimal().Should().Be(150000m);
        body.GetProperty("roiPercent").GetDecimal().Should().Be(150m);
        DateTimeOffset.Parse(body.GetProperty("targetDate").GetString()!).UtcDateTime.Should().Be(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        yield return ["""{"division":"LOGISTICA"}""", "title"];
        yield return ["""{"title":"   ","division":"LOGISTICA"}""", "title"];
        yield return ["""{"title":"Projeto"}""", "division"];
        yield return ["""{"title":"Projeto","division":"LOGISTICA","investment":-1}""", "investment"];
        yield return ["""{"title":"Projeto","division":"LOGISTICA","financialReturn":-5}""", "financialReturn"];
        yield return ["""{"title":"Projeto","division":"LOGISTICA","costReduction":-5}""", "costReduction"];
        yield return ["""{"title":"Projeto","division":"LOGISTICA","productivityGain":-5}""", "productivityGain"];
        yield return ["""{"title":"Projeto","division":"LOGISTICA","investment":9999999999999}""", "investment"];
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_InvalidBody_Returns400_WithTheField(string json, string field)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var response = await gestor.PostAsync(Base, Raw(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
    }

    [Theory]
    [InlineData("""{"title":"Projeto","division":"MARTE"}""")]
    [InlineData("""{"title":"Projeto","division":"LOGISTICA","stage":"NAO_EXISTE"}""")]
    [InlineData("""{"title":"Projeto","division":"LOGISTICA","investment":"muito"}""")]
    public async Task Create_UnparsableValues_Return400(string json)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.PostAsync(Base, Raw(json))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownGuidelineOrResponsible_Returns422()
    {
        using var gestor = api.ClientAs(Role.GESTOR);

        var g = await gestor.PostAsJsonAsync(Base, Body(guidelineId: "665f00000000000000000abc"));
        (g.StatusCode, (await Json(g)).GetProperty("code").GetString()).Should().Be((HttpStatusCode.UnprocessableEntity, "GUIDELINE_NOT_FOUND"));

        var r = await gestor.PostAsJsonAsync(Base, Body(responsibleId: "665f00000000000000000abc"));
        (r.StatusCode, (await Json(r)).GetProperty("code").GetString()).Should().Be((HttpStatusCode.UnprocessableEntity, "RESPONSIBLE_NOT_FOUND"));

        // um operador não pode ser responsável
        var op = (await Json(await gestor.GetAsync("/api/v1/users?role=OPERADOR"))).EnumerateArray().First().GetProperty("id").GetString();
        (await gestor.PostAsJsonAsync(Base, Body(responsibleId: op))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // =============================== EDITAR / HISTÓRICO ===============================

    [Fact]
    public async Task Update_InvestmentChange_ProducesExactlyOneTypedDiff_AndAnEntryWithTheNote()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor, Body(investment: 100000));
        var id = created.GetProperty("id").GetString()!;

        var response = await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: created.GetProperty("title").GetString(), investment: 120000, note: "Ajuste do orçamento"));
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("investment").GetDecimal().Should().Be(120000m);
        body.GetProperty("version").GetInt32().Should().Be(2);

        var history = await UpdatesOf(gestor, id);
        history.Select(h => h.GetProperty("note").GetString()).Should().Equal("Ajuste do orçamento", "Projeto criado");
        var change = history[0].GetProperty("changes").EnumerateArray().Should().ContainSingle().Subject;
        change.GetProperty("field").GetString().Should().Be("investment");
        change.GetProperty("from").ValueKind.Should().Be(JsonValueKind.Number);
        (change.GetProperty("from").GetDecimal(), change.GetProperty("to").GetDecimal()).Should().Be((100000m, 120000m));
    }

    [Fact]
    public async Task Update_AnyChangedFields_AreDiffed_WithTextNumberDateAndNullTypes()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor);
        var id = created.GetProperty("id").GetString()!;

        await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: "Título novo", stage: "EM_EXECUCAO", financialReturn: 500, targetDate: "2026-12-31", statusText: "Piloto"));

        var changes = (await UpdatesOf(gestor, id))[0].GetProperty("changes").EnumerateArray().ToDictionary(c => c.GetProperty("field").GetString()!);
        changes.Keys.Should().BeEquivalentTo(["title", "stage", "financialReturn", "targetDate", "statusText"]);
        changes["stage"].GetProperty("to").GetString().Should().Be("EM_EXECUCAO");
        changes["financialReturn"].GetProperty("to").GetDecimal().Should().Be(500m);
        changes["targetDate"].GetProperty("from").ValueKind.Should().Be(JsonValueKind.Null);
        changes["targetDate"].GetProperty("to").GetString().Should().Be("2026-12-31T00:00:00Z");
    }

    [Fact]
    public async Task Update_WithoutRealChanges_StillLogsTheNote_WithNoChanges_AndBumpsTheVersion()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor);
        var id = created.GetProperty("id").GetString()!;

        var response = await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: created.GetProperty("title").GetString(), note: "Apenas um registro"));

        (await Json(response)).GetProperty("version").GetInt32().Should().Be(2);
        var entry = (await UpdatesOf(gestor, id))[0];
        entry.GetProperty("note").GetString().Should().Be("Apenas um registro");
        entry.GetProperty("changes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Update_MovesTheProjectToTheTopOfTheList()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var target = await CreateAsync(gestor);
        await CreateAsync(gestor);
        await Task.Delay(20);
        var id = target.GetProperty("id").GetString()!;

        await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: "Reposicionado", investment: 1));

        using var lider = api.ClientAs(Role.LIDER);
        (await Json(await lider.GetAsync(Base))).GetProperty("items")[0].GetProperty("id").GetString().Should().Be(id);
    }

    [Fact]
    public async Task Update_ChangingTheResponsible_IsRecordedByName()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor);
        var id = created.GetProperty("id").GetString()!;
        var lider = (await Json(await gestor.GetAsync("/api/v1/users?role=LIDER"))).EnumerateArray().First();

        await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: created.GetProperty("title").GetString(), responsibleId: lider.GetProperty("id").GetString()));

        var change = (await UpdatesOf(gestor, id))[0].GetProperty("changes").EnumerateArray().Single();
        change.GetProperty("field").GetString().Should().Be("responsável");
        change.GetProperty("to").GetString().Should().Be(lider.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("""{"title":"Projeto","division":"LOGISTICA","stage":"PLANEJAMENTO"}""", "investment")]
    [InlineData("""{"title":"Projeto","division":"LOGISTICA","investment":1,"financialReturn":1,"costReduction":1,"productivityGain":1}""", "stage")]
    [InlineData("""{"title":"Projeto","stage":"PLANEJAMENTO","investment":1,"financialReturn":1,"costReduction":1,"productivityGain":1}""", "division")]
    public async Task Put_IsAFullReplacement_MissingRequiredFields_Return400(string json, string field)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateAsync(gestor, Body(investment: 777))).GetProperty("id").GetString()!;

        var response = await gestor.PutAsync($"{Base}/{id}", Raw(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
        (await Json(await gestor.GetAsync($"{Base}/{id}"))).GetProperty("investment").GetDecimal().Should().Be(777m, "nada foi zerado");
    }

    // =============================== CONCORRÊNCIA ===============================

    [Fact]
    public async Task StaleVersion_Returns409_AndNothingIsWritten()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor, Body(investment: 1));
        var id = created.GetProperty("id").GetString()!;
        var title = created.GetProperty("title").GetString();
        await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: title, investment: 2));               // versão 2

        var stale = await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: title, investment: 3, version: 1));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(stale)).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        (await Json(await gestor.GetAsync($"{Base}/{id}"))).GetProperty("investment").GetDecimal().Should().Be(2m);
        (await UpdatesOf(gestor, id)).Should().HaveCount(2, "criação + a edição bem-sucedida");

        var ok = await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: title, investment: 4, version: 2));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SimultaneousEditsWithTheSameVersion_OnlyOneWins_TheRestGet409()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor, Body(investment: 1));
        var id = created.GetProperty("id").GetString()!;
        var title = created.GetProperty("title").GetString();

        var responses = await Task.WhenAll(Enumerable.Range(1, 6).Select(i =>
            gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: title, investment: 100 + i, version: 1))));
        var codes = responses.Select(r => r.StatusCode).ToList();

        codes.Count(c => c == HttpStatusCode.OK).Should().Be(1, string.Join(",", codes));
        codes.Count(c => c == HttpStatusCode.Conflict).Should().Be(5);
        (await Json(await gestor.GetAsync($"{Base}/{id}"))).GetProperty("version").GetInt32().Should().Be(2);
        (await UpdatesOf(gestor, id)).Should().HaveCount(2, "só a edição vencedora deixa histórico");
    }

    // =============================== LEITURA ===============================

    [Fact]
    public async Task List_ShowsTheSeededProjects_WithFiltersAndPaging()
    {
        using var lider = api.ClientAs(Role.LIDER);

        var all = (await Json(await lider.GetAsync($"{Base}?pageSize=200"))).GetProperty("items").EnumerateArray().ToList();
        all.Select(p => p.GetProperty("stage").GetString()).Should().Contain(["PLANEJAMENTO", "EM_EXECUCAO", "CONCLUIDO"]);
        all.Select(p => p.GetProperty("updatedAt").GetString()!).Select(s => DateTimeOffset.Parse(s)).Should().BeInDescendingOrder();

        var done = (await Json(await lider.GetAsync($"{Base}?stage=CONCLUIDO&pageSize=200"))).GetProperty("items").EnumerateArray();
        done.Should().OnlyContain(p => p.GetProperty("stage").GetString() == "CONCLUIDO").And.NotBeEmpty();

        var logistica = (await Json(await lider.GetAsync($"{Base}?division=LOGISTICA&pageSize=200"))).GetProperty("items").EnumerateArray();
        logistica.Should().OnlyContain(p => p.GetProperty("division").GetString() == "LOGISTICA");

        var g = all.First(p => p.GetProperty("guidelineId").ValueKind == JsonValueKind.String).GetProperty("guidelineId").GetString();
        (await Json(await lider.GetAsync($"{Base}?guidelineId={g}&pageSize=200"))).GetProperty("items").EnumerateArray()
            .Should().OnlyContain(p => p.GetProperty("guidelineId").GetString() == g);

        var page = await Json(await lider.GetAsync($"{Base}?page=2&pageSize=1"));
        page.GetProperty("items").GetArrayLength().Should().Be(1);
        page.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    [Theory]
    [InlineData("stage=NADA")]
    [InlineData("division=MARTE")]
    [InlineData("page=0")]
    [InlineData("pageSize=201")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.GetAsync($"{Base}?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SeededCompletedProject_ExposesFinancials_AndARichTimeline()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var done = await SeededProjectAsync(lider, "PROJ: Painel de indicadores de pontualidade");
        var id = done.GetProperty("id").GetString()!;

        done.GetProperty("investment").GetDecimal().Should().Be(120000m);
        done.GetProperty("financialReturn").GetDecimal().Should().Be(310000m);
        done.GetProperty("netProfit").GetDecimal().Should().Be(190000m);
        Math.Round(done.GetProperty("roiPercent").GetDecimal(), 1).Should().Be(158.3m);
        done.GetProperty("originatingIdeaId").GetString().Should().NotBeNullOrEmpty();
        done.GetProperty("reporterName").GetString().Should().Be("Operador INOVAGAB");

        var timeline = await UpdatesOf(lider, id);
        timeline.Select(t => t.GetProperty("note").GetString()).Should().Equal(
            "Meta atingida", "Primeiros ganhos medidos", "Início da execução", "Criado automaticamente a partir da ideia: Painel de indicadores de pontualidade");
        var last = timeline[0].GetProperty("changes").EnumerateArray().ToDictionary(c => c.GetProperty("field").GetString()!);
        last["stage"].GetProperty("from").GetString().Should().Be("EM_EXECUCAO");
        last["stage"].GetProperty("to").GetString().Should().Be("CONCLUIDO");
        last["financialReturn"].GetProperty("from").GetDecimal().Should().Be(90000m);
        last["financialReturn"].GetProperty("to").GetDecimal().Should().Be(310000m);
        last["costReduction"].GetProperty("to").GetDecimal().Should().Be(45000m);
    }

    [Fact]
    public async Task Updates_ArePaged_AndValidated()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var id = (await SeededProjectAsync(lider, "PROJ: Painel de indicadores de pontualidade")).GetProperty("id").GetString();

        var page = await Json(await lider.GetAsync($"{Base}/{id}/updates?page=2&pageSize=3"));

        page.GetProperty("items").GetArrayLength().Should().Be(1);
        (page.GetProperty("totalItems").GetInt32(), page.GetProperty("totalPages").GetInt32()).Should().Be((4, 2));
        (await lider.GetAsync($"{Base}/{id}/updates?pageSize=500")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("nao-e-objectid")]
    [InlineData("665f00000000000000000abc")]
    public async Task UnknownOrMalformedIds_Return404_OnEveryRoute(string id)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.GetAsync($"{Base}/{id}/updates")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.PutAsJsonAsync($"{Base}/{id}", Body())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =============================== EXCLUIR ===============================

    [Fact]
    public async Task Delete_RemovesTheProjectAndItsHistory()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateAsync(gestor)).GetProperty("id").GetString()!;
        await gestor.PutAsJsonAsync($"{Base}/{id}", Body(title: "Antes de excluir", investment: 5));

        (await gestor.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await gestor.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.GetAsync($"{Base}/{id}/updates")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.Db.Raw(Collections.ProjectUpdates).CountDocumentsAsync(new BsonDocument("projectId", new ObjectId(id)))).Should().Be(0);
        (await gestor.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingTheProject_LeavesTheOriginatingIdeaApproved()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var ideaId = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;
        var projectId = (await Json(await gestor.PostAsync($"/api/v1/ideas/{ideaId}/approve", null))).GetProperty("projectId").GetString()!;

        (await gestor.DeleteAsync($"{Base}/{projectId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var idea = await GetIdeaAsync(op, ideaId);
        idea.GetProperty("status").GetString().Should().Be("APROVADA");
        idea.GetProperty("linkedProject").ValueKind.Should().Be(JsonValueKind.Null);
        (await Points(op)).Should().Be(60, "os pontos já concedidos não são estornados");
    }

    // =============================== B16: CONCLUSÃO ===============================

    private async Task<(HttpClient Op, HttpClient Gestor, string IdeaId, string ProjectId)> ApprovedIdeaAsync()
    {
        var op = await NewUserClientAsync(api, name: "Operadora Conclusão");
        var gestor = api.ClientAs(Role.GESTOR);
        var ideaId = (await CreateIdeaAsync(op, await AnyGuidelineIdAsync(api), "Ideia que será implementada")).GetProperty("id").GetString()!;
        await gestor.PutAsJsonAsync($"/api/v1/ideas/{ideaId}/ice", new { impact = 9, confidence = 9, ease = 8 });
        var projectId = (await Json(await gestor.PostAsync($"/api/v1/ideas/{ideaId}/approve", null))).GetProperty("projectId").GetString()!;
        return (op, gestor, ideaId, projectId);
    }

    private static object Conclude(string projectTitle, string stage = "CONCLUIDO", string? note = null) =>
        Body(title: projectTitle, stage: stage, investment: 100000, financialReturn: 300000, productivityGain: 12.5m, costReduction: 40000,
            note: note, statusText: "Entregue");

    [Fact]
    public async Task CompletingTheProject_ImplementsTheIdea_Awards200_AndGrantsImpactoReal()
    {
        var (op, gestor, ideaId, projectId) = await ApprovedIdeaAsync();
        using (op) using (gestor)
        {
            (await Points(op)).Should().Be(15 + 50);
            var title = (await Json(await gestor.GetAsync($"{Base}/{projectId}"))).GetProperty("title").GetString()!;

            var response = await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title, note: "Meta atingida"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await Json(response)).GetProperty("stage").GetString().Should().Be("CONCLUIDO");

            var idea = await GetIdeaAsync(op, ideaId);
            idea.GetProperty("status").GetString().Should().Be("IMPLEMENTADA");
            idea.GetProperty("linkedProject").GetProperty("stage").GetString().Should().Be("CONCLUIDO");

            (await Points(op)).Should().Be(15 + 50 + 200);
            (await Badges(op)).Should().BeEquivalentTo(["Primeira Ideia", "Estrategista", "Impacto Real"]);

            var opId = (await Me(op)).GetProperty("id").GetString();
            var ranking = (await Json(await op.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray()
                .Single(r => r.GetProperty("id").GetString() == opId);
            ranking.GetProperty("monthPoints").GetInt32().Should().Be(265);

            var events = await api.Db.Raw(Collections.PointEvents).CountDocumentsAsync(
                new BsonDocument { { "reason", "IDEA_IMPLEMENTED" }, { "refId", new ObjectId(ideaId) } });
            events.Should().Be(1);
        }
    }

    [Fact]
    public async Task RepeatingTheCompletion_AndReopeningAndClosingAgain_NeverCreditsTwice()
    {
        var (op, gestor, _, projectId) = await ApprovedIdeaAsync();
        using (op) using (gestor)
        {
            var title = (await Json(await gestor.GetAsync($"{Base}/{projectId}"))).GetProperty("title").GetString()!;
            await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title));
            var after = await Points(op);

            (await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title))).StatusCode.Should().Be(HttpStatusCode.OK);
            await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title, "EM_EXECUCAO"));
            await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title));

            (await Points(op)).Should().Be(after).And.Be(265);
        }
    }

    [Fact]
    public async Task OtherStageChanges_LeaveTheIdeaApproved_AndPointsUntouched()
    {
        var (op, gestor, ideaId, projectId) = await ApprovedIdeaAsync();
        using (op) using (gestor)
        {
            var title = (await Json(await gestor.GetAsync($"{Base}/{projectId}"))).GetProperty("title").GetString()!;

            await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title, "EM_EXECUCAO"));
            await gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title, "CANCELADO"));

            (await GetIdeaAsync(op, ideaId)).GetProperty("status").GetString().Should().Be("APROVADA");
            (await Points(op)).Should().Be(65);
        }
    }

    [Fact]
    public async Task SimultaneousCompletions_CreditOnce_AndOnlyOneEntryCarriesTheStageChange()
    {
        var (op, gestor, ideaId, projectId) = await ApprovedIdeaAsync();
        using (op) using (gestor)
        {
            var title = (await Json(await gestor.GetAsync($"{Base}/{projectId}"))).GetProperty("title").GetString()!;

            var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
                gestor.PutAsJsonAsync($"{Base}/{projectId}", Conclude(title, note: $"Conclusão {i}"))));
            var bodies = await Task.WhenAll(responses.Select(Json));

            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK, string.Join(" | ", bodies.Select(b => b.GetRawText())));
            (await GetIdeaAsync(op, ideaId)).GetProperty("status").GetString().Should().Be("IMPLEMENTADA");
            (await Points(op)).Should().Be(15 + 50 + 200, "+200 uma única vez");
            (await api.Db.Raw(Collections.PointEvents).CountDocumentsAsync(
                new BsonDocument { { "reason", "IDEA_IMPLEMENTED" }, { "refId", new ObjectId(ideaId) } })).Should().Be(1);

            var withStageChange = (await UpdatesOf(gestor, projectId))
                .Count(u => u.GetProperty("changes").EnumerateArray().Any(c => c.GetProperty("field").GetString() == "stage"
                                                                              && c.GetProperty("to").GetString() == "CONCLUIDO"));
            withStageChange.Should().Be(1, "só a edição que efetivamente concluiu registra a mudança de estágio");
        }
    }

    [Fact]
    public async Task ADirectProject_CanBeCompleted_WithoutAnyIdeaEffect()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var created = await CreateAsync(gestor);
        var pointsBefore = await api.Db.Raw(Collections.PointEvents).CountDocumentsAsync(new BsonDocument("reason", "IDEA_IMPLEMENTED"));

        var response = await gestor.PutAsJsonAsync($"{Base}/{created.GetProperty("id").GetString()}", Conclude(created.GetProperty("title").GetString()!));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Db.Raw(Collections.PointEvents).CountDocumentsAsync(new BsonDocument("reason", "IDEA_IMPLEMENTED"))).Should().Be(pointsBefore);
    }
}
