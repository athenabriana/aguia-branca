using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.FirestoreMigrator.Migration;
using AguiaBranca.FirestoreMigrator.Tests;
using AguiaBranca.Infrastructure.Tests.Support;
using MongoDB.Bson;
using static AguiaBranca.Api.Tests.Support.ApiTestKit;

namespace AguiaBranca.Api.Tests.Migration;

/// <summary>
/// B23 — prova de compatibilidade: os documentos gravados pelo migrador (MongoDB real) são lidos <b>e escritos</b> pela API
/// como se fossem nativos. Migra o dataset fictício e exercita login com a senha temporária, leituras, relatórios e novas
/// operações (aprovar ideia migrada, editar projeto migrado).
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationCompatibilityTests(MongoFixture mongo) : ApiIntegrationTest(mongo)
{
    private const string TempPassword = "senha-temporaria-1";

    private async Task<MigrationReport> MigrateAsync(InMemorySource? source = null, MongoSink? sink = null)
    {
        var report = await new MigrationRunner(source ?? Sample.Full(), sink ?? new MongoSink(Db.Database), Sample.Options()).RunAsync(default);
        report.Reconciled.Should().BeTrue(report.ToConsole());
        return report;
    }

    private async Task<HttpClient> ClientOf(string email)
    {
        var login = await LoginAsync(email, TempPassword);
        login.Response.StatusCode.Should().Be(HttpStatusCode.OK, login.Body.GetRawText());
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    [Fact]
    public async Task MigratedUsers_LogInWithTheTemporaryPassword_AndKeepTheirProfileBadgesAndPoints()
    {
        await MigrateAsync();

        (await LoginAsync("gestor@x.com", "outra-senha-qualquer")).Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var op = await ClientOf("op@x.com");

        var me = await Me(op);
        (me.GetProperty("name").GetString(), me.GetProperty("role").GetString(), me.GetProperty("points").GetInt32()).Should().Be(("Operadora", "OPERADOR", 265));
        me.GetProperty("badges").EnumerateArray().Select(b => b.GetString()).Should().BeEquivalentTo("Primeira Ideia", "Estrategista", "Impacto Real");
    }

    [Fact]
    public async Task ApiReadsEveryMigratedCollection_WithRemappedReferencesAndOriginalValues()
    {
        await MigrateAsync();
        using var lider = await ClientOf("lider@x.com");

        var ideas = (await Json(await lider.GetAsync("/api/v1/ideas?scope=all&pageSize=50"))).GetProperty("items").EnumerateArray().ToList();
        ideas.Should().HaveCount(3);
        var implemented = ideas.Single(i => i.GetProperty("status").GetString() == "IMPLEMENTADA");
        implemented.GetProperty("guidelineTitle").GetString().Should().Be("Eficiência");
        implemented.GetProperty("ice").GetProperty("score").GetInt32().Should().Be(648);
        implemented.GetProperty("linkedProject").GetProperty("stage").GetString().Should().Be("CONCLUIDO");
        implemented.GetProperty("authorName").GetString().Should().Be("Autor");

        var projects = (await Json(await lider.GetAsync("/api/v1/projects?pageSize=50"))).GetProperty("items").EnumerateArray().ToList();
        var p1 = projects.Single(p => p.GetProperty("title").GetString() == "PROJ: Ideia implementada");
        (p1.GetProperty("investment").GetDecimal(), p1.GetProperty("financialReturn").GetDecimal(), p1.GetProperty("productivityGain").GetDecimal(),
            p1.GetProperty("roiPercent").GetDecimal(), p1.GetProperty("guidelineTitle").GetString()).Should().Be((1000m, 2500m, 10.5m, 150m, "Eficiência"));

        var timeline = (await Json(await lider.GetAsync($"/api/v1/projects/{p1.GetProperty("id").GetString()}/updates"))).GetProperty("items").EnumerateArray().ToList();
        timeline.Should().ContainSingle().Which.GetProperty("changes")[0].Should().Match<System.Text.Json.JsonElement>(c =>
            c.GetProperty("field").GetString() == "stage" && c.GetProperty("to").GetString() == "CONCLUIDO");

        var history = (await Json(await lider.GetAsync("/api/v1/guidelines/history"))).GetProperty("items").EnumerateArray().ToList();
        history.Should().HaveCount(2).And.OnlyContain(h => h.GetProperty("action").GetString() == "CREATED");

        var guidelines = (await Json(await lider.GetAsync("/api/v1/guidelines"))).GetProperty("items").EnumerateArray().Select(g => g.GetProperty("title").GetString());
        guidelines.Should().BeEquivalentTo("Eficiência", "Cliente");
    }

    [Fact]
    public async Task Dashboard_ComputesOverTheMigratedData()
    {
        await MigrateAsync();
        using var lider = await ClientOf("lider@x.com");

        var s = await Json(await lider.GetAsync("/api/v1/reports/summary"));

        s.GetProperty("funnel").GetProperty("submitted").GetInt32().Should().Be(3);
        (s.GetProperty("funnel").GetProperty("approved").GetInt32(), s.GetProperty("funnel").GetProperty("roiPositive").GetInt32()).Should().Be((1, 1));
        s.GetProperty("kpis").GetProperty("totalInvestment").GetDecimal().Should().Be(1000m);
        s.GetProperty("kpis").GetProperty("totalReturn").GetDecimal().Should().Be(2500m);
        s.GetProperty("kpis").GetProperty("roiConsolidated").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task NewOperationsWorkOnMigratedData_ApprovingAMigratedIdea_AwardsPointsOnTopOfTheOpeningBalance()
    {
        await MigrateAsync();
        using var gestor = await ClientOf("gestor@x.com");
        using var op = await ClientOf("op@x.com");
        var ideas = (await Json(await gestor.GetAsync("/api/v1/ideas?scope=all&pageSize=50"))).GetProperty("items").EnumerateArray().ToList();
        var submitted = ideas.Single(i => i.GetProperty("title").GetString() == "Ideia submetida").GetProperty("id").GetString()!;

        (await gestor.PutAsJsonAsync($"/api/v1/ideas/{submitted}/ice", new { impact = 7, confidence = 7, ease = 7 })).StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await gestor.PostAsync($"/api/v1/ideas/{submitted}/approve", null);

        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await Points(op)).Should().Be(265 + 50);
        (await Json(await gestor.GetAsync($"/api/v1/projects/{(await Json(approved)).GetProperty("projectId").GetString()}")))
            .GetProperty("originatingIdeaId").GetString().Should().Be(submitted);
    }

    [Fact]
    public async Task EditingAMigratedProject_WritesHistory_AndBumpsTheVersion()
    {
        await MigrateAsync();
        using var gestor = await ClientOf("gestor@x.com");
        var p2 = (await Json(await gestor.GetAsync("/api/v1/projects?pageSize=50"))).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("title").GetString() == "Projeto avulso");
        var id = p2.GetProperty("id").GetString()!;

        var put = await gestor.PutAsJsonAsync($"/api/v1/projects/{id}", new
        {
            title = "Projeto avulso", description = "d", stage = "EM_EXECUCAO", statusText = "ok", investment = 5000, targetDate = (string?)null,
            financialReturn = 0, productivityGain = 0, costReduction = 0, division = "LOGISTICA", guidelineId = (string?)null, note = "Pós-migração"
        });

        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        (await Json(put)).GetProperty("version").GetInt32().Should().Be(2);
        (await Json(await gestor.GetAsync($"/api/v1/projects/{id}/updates"))).GetProperty("items").GetArrayLength().Should().Be(2, "1 migrada + 1 nova");
    }

    [Fact]
    public async Task RunningTheMigrationTwiceOnTheRealDatabase_DoesNotDuplicateAnything()
    {
        await MigrateAsync();
        var counts = async () => (await Task.WhenAll(new[] { "users", "guidelines", "guidelineHistory", "ideas", "projects", "projectUpdates", "pointEvents" }
            .Select(c => Db.Raw(c).CountDocumentsAsync(new BsonDocument())))).ToArray();
        var before = await counts();

        var second = await MigrateAsync();

        (await counts()).Should().Equal(before);
        before.Should().Equal(3L, 2L, 2L, 3L, 2L, 2L, 1L);
        second.Collections.Should().OnlyContain(c => c.VerifiedInDestination == c.Migrated);
        (await Db.Raw(MongoSink.IdMapCollection).CountDocumentsAsync(new BsonDocument())).Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task Migration_RespectsTheUniqueEmailIndex_ByReportingAConflictInsteadOfFailing()
    {
        await CreateUserAsync("op@x.com", name: "Já existia");

        var report = await new MigrationRunner(Sample.Full(), new MongoSink(Db.Database), Sample.Options()).RunAsync(default);

        report.Issues.Should().Contain(i => i.Kind == IssueKind.Conflict && i.LegacyId == "u-op");
        (await Db.Raw("users").CountDocumentsAsync(new BsonDocument("normalizedEmail", "OP@X.COM"))).Should().Be(1);
    }
}
