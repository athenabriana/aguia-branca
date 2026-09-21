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

namespace AguiaBranca.Api.Tests.Ideas;

[Trait("Category", "Integration")]
public sealed class IdeaReviewApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string Base = "/api/v1/ideas";

    private static StringContent Raw(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<string> SeededIdeaIdAsync(string status)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        return (await Json(await gestor.GetAsync($"{Base}?status={status}&pageSize=1"))).GetProperty("items")[0].GetProperty("id").GetString()!;
    }

    private async Task<BsonDocument?> ProjectOfAsync(string ideaId) =>
        await api.Db.Raw(Collections.Projects).Find(new BsonDocument("originatingIdeaId", new ObjectId(ideaId))).FirstOrDefaultAsync();

    private async Task<long> ProjectCountAsync(string ideaId) =>
        await api.Db.Raw(Collections.Projects).CountDocumentsAsync(new BsonDocument("originatingIdeaId", new ObjectId(ideaId)));

    // =============================== ICE ===============================

    [Fact]
    public async Task Gestor_SavesIce_ScoreIsComputed_AndTheIdeaMovesToEmAnalise()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        var response = await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 8, confidence = 7, ease = 6, score = 9999 });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("EM_ANALISE");
        var ice = body.GetProperty("ice");
        (ice.GetProperty("impact").GetInt32(), ice.GetProperty("confidence").GetInt32(), ice.GetProperty("ease").GetInt32()).Should().Be((8, 7, 6));
        ice.GetProperty("score").GetInt32().Should().Be(336, "o score é do servidor; o enviado é ignorado");
        body.GetProperty("reviewerId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AfterTheFirstIce_TheAuthorCanNoLongerEditOrDelete()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;
        await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 5, confidence = 5, ease = 5 });

        var edit = await op.PutAsJsonAsync($"{Base}/{id}", new { title = "Tentativa tardia", category = "tecnologia" });
        var delete = await op.DeleteAsync($"{Base}/{id}");

        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(edit)).GetProperty("code").GetString().Should().Be("IDEA_NOT_EDITABLE");
        (await GetIdeaAsync(op, id)).GetProperty("status").GetString().Should().Be("EM_ANALISE");
    }

    [Fact]
    public async Task SavingAnotherIce_Replaces_AndTheIdeaAppearsInCurationOrderedByScore()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;
        await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 1, confidence = 1, ease = 1 });

        var second = await Json(await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 10, confidence = 10, ease = 10 }));

        second.GetProperty("ice").GetProperty("score").GetInt32().Should().Be(1000);
        var curation = (await Json(await gestor.GetAsync($"{Base}?scope=curation&pageSize=200"))).GetProperty("items");
        curation[0].GetProperty("id").GetString().Should().Be(id, "score 1000 é o maior possível");
    }

    public static IEnumerable<object[]> InvalidIce()
    {
        yield return ["""{"impact":0,"confidence":5,"ease":5}""", "impact"];
        yield return ["""{"impact":5,"confidence":11,"ease":5}""", "confidence"];
        yield return ["""{"impact":5,"confidence":5,"ease":-3}""", "ease"];
        yield return ["""{"confidence":5,"ease":5}""", "impact"];
        yield return ["""{"impact":5,"ease":5}""", "confidence"];
        yield return ["""{"impact":5,"confidence":5}""", "ease"];
    }

    [Theory]
    [MemberData(nameof(InvalidIce))]
    public async Task InvalidIce_Returns400_WithTheField_AndTheIdeaStaysSubmitted(string json, string field)
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        var response = await gestor.PutAsync($"{Base}/{id}/ice", Raw(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
        (await GetIdeaAsync(op, id)).GetProperty("status").GetString().Should().Be("SUBMETIDA");
    }

    [Theory]
    [InlineData("""{"impact":7.5,"confidence":5,"ease":5}""")]
    [InlineData("""{"impact":"alto","confidence":5,"ease":5}""")]
    public async Task NonIntegerIce_Returns400(string json)
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        (await gestor.PutAsync($"{Base}/{id}/ice", Raw(json))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ice_OnAnApprovedIdea_Returns409InvalidState()
    {
        var approved = await SeededIdeaIdAsync("APROVADA");
        using var gestor = api.ClientAs(Role.GESTOR);

        var response = await gestor.PutAsJsonAsync($"{Base}/{approved}/ice", new { impact = 5, confidence = 5, ease = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("code").GetString().Should().Be("IDEA_INVALID_STATE");
    }

    [Theory]
    [InlineData(Role.OPERADOR, HttpStatusCode.Forbidden)]
    [InlineData(Role.LIDER, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Ice_OnlyGestor(Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);
        var response = await client.PutAsJsonAsync($"{Base}/665f00000000000000000abc/ice", new { impact = 5, confidence = 5, ease = 5 });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Ice_OnUnknownIdea_Returns404()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.PutAsJsonAsync($"{Base}/665f00000000000000000abc/ice", new { impact = 5, confidence = 5, ease = 5 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =============================== REJEITAR ===============================

    [Fact]
    public async Task Reject_WithComment_MarksRejected_AndTheAuthorGetsNoPoints()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;
        var pointsBefore = await Points(op);

        var response = await gestor.PostAsJsonAsync($"{Base}/{id}/reject", new { comment = "Já existe iniciativa parecida." });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("REJEITADA");
        body.GetProperty("reviewComment").GetString().Should().Be("Já existe iniciativa parecida.");
        body.GetProperty("reviewedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await Points(op)).Should().Be(pointsBefore);

        var seenByAuthor = await GetIdeaAsync(op, id);
        seenByAuthor.GetProperty("status").GetString().Should().Be("REJEITADA");
        seenByAuthor.GetProperty("reviewComment").GetString().Should().Be("Já existe iniciativa parecida.");
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"comment":""}""")]
    [InlineData("""{"comment":"    "}""")]
    public async Task Reject_WithoutComment_Returns400(string json)
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        var response = await gestor.PostAsync($"{Base}/{id}/reject", Raw(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == "comment");
        (await GetIdeaAsync(op, id)).GetProperty("status").GetString().Should().Be("SUBMETIDA");
    }

    [Fact]
    public async Task AfterRejection_NothingElseCanHappen()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;
        await gestor.PostAsJsonAsync($"{Base}/{id}/reject", new { comment = "não" });

        (await gestor.PostAsJsonAsync($"{Base}/{id}/reject", new { comment = "de novo" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 5, confidence = 5, ease = 5 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var approve = await gestor.PostAsync($"{Base}/{id}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(approve)).GetProperty("code").GetString().Should().Be("IDEA_INVALID_STATE");
        (await ProjectCountAsync(id)).Should().Be(0);
    }

    [Theory]
    [InlineData(Role.OPERADOR, HttpStatusCode.Forbidden)]
    [InlineData(Role.LIDER, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Reject_OnlyGestor(Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);
        (await client.PostAsJsonAsync($"{Base}/665f00000000000000000abc/reject", new { comment = "x" })).StatusCode.Should().Be(expected);
    }

    // =============================== APROVAR ===============================

    [Fact]
    public async Task Approve_CreatesTheDraftProject_InheritingFromTheIdea_AndAwards50()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var gestorId = (await Me(gestor)).GetProperty("id").GetString();
        var opId = (await Me(op)).GetProperty("id").GetString();
        var guidelineId = await AnyGuidelineIdAsync(api);
        var idea = await CreateIdeaAsync(op, guidelineId, "Roteirização dinâmica");
        var id = idea.GetProperty("id").GetString()!;
        await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 9, confidence = 8, ease = 7 });

        var response = await gestor.PostAsync($"{Base}/{id}/approve", null);
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("ideaId").GetString().Should().Be(id);
        body.GetProperty("alreadyApproved").GetBoolean().Should().BeFalse();
        var projectId = body.GetProperty("projectId").GetString()!;

        var project = (await ProjectOfAsync(id))!;
        project["_id"].AsObjectId.ToString().Should().Be(projectId);
        project["title"].AsString.Should().Be("PROJ: Roteirização dinâmica");
        project["stage"].AsString.Should().Be("PLANEJAMENTO");
        project["division"].AsString.Should().Be("LOGISTICA");
        project["guidelineId"].AsObjectId.ToString().Should().Be(guidelineId);
        project["creatorManagerId"].AsObjectId.ToString().Should().Be(gestorId);
        project["responsibleId"].AsObjectId.ToString().Should().Be(gestorId);
        project["reporterId"].AsObjectId.ToString().Should().Be(opId);
        project["priorityScore"].AsInt32.Should().Be(504);
        project["investment"].ToDecimal().Should().Be(0m);
        project["version"].AsInt32.Should().Be(1);

        var history = await api.Db.Raw(Collections.ProjectUpdates).Find(new BsonDocument("projectId", new ObjectId(projectId))).ToListAsync();
        history.Should().ContainSingle();
        history[0]["note"].AsString.Should().Be("Criado automaticamente a partir da ideia: Roteirização dinâmica");
        history[0]["authorId"].AsObjectId.ToString().Should().Be(gestorId);
        history[0]["changes"].AsBsonArray.Should().BeEmpty();

        (await Points(op)).Should().Be(15 + 50);
        (await Badges(op)).Should().BeEquivalentTo(["Primeira Ideia", "Estrategista"]);
    }

    [Fact]
    public async Task Approve_WithoutIce_IsAllowed_PriorityScoreIsNull()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        (await gestor.PostAsync($"{Base}/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ProjectOfAsync(id))!["priorityScore"].IsBsonNull.Should().BeTrue();
        (await Points(op)).Should().Be(10 + 50);
        (await Badges(op)).Should().Equal("Primeira Ideia");
    }

    [Fact]
    public async Task Approve_Twice_IsIdempotent_SameProject_PointsOnlyOnce()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        var first = await Json(await gestor.PostAsync($"{Base}/{id}/approve", null));
        var secondResponse = await gestor.PostAsync($"{Base}/{id}/approve", null);
        var second = await Json(secondResponse);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        second.GetProperty("alreadyApproved").GetBoolean().Should().BeTrue();
        second.GetProperty("projectId").GetString().Should().Be(first.GetProperty("projectId").GetString());
        (await ProjectCountAsync(id)).Should().Be(1);
        (await Points(op)).Should().Be(10 + 50);
    }

    [Fact]
    public async Task ManySimultaneousApprovals_YieldOneProject_OneAward_AndOnlyOneFreshApproval()
    {
        using var op = await NewUserClientAsync(api);
        using var gestor = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(op)).GetProperty("id").GetString()!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => gestor.PostAsync($"{Base}/{id}/approve", null)));
        var bodies = await Task.WhenAll(responses.Select(Json));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK, string.Join(" | ", bodies.Select(b => b.GetRawText())));
        bodies.Count(b => !b.GetProperty("alreadyApproved").GetBoolean()).Should().Be(1, "só uma requisição vence a corrida");
        bodies.Select(b => b.GetProperty("projectId").GetString()).Distinct().Should().ContainSingle().Which.Should().NotBeNull();
        (await ProjectCountAsync(id)).Should().Be(1);
        (await Points(op)).Should().Be(10 + 50, "+50 uma única vez");
        var approvedEvents = await api.Db.Raw(Collections.PointEvents)
            .CountDocumentsAsync(new BsonDocument { { "reason", "IDEA_APPROVED" }, { "refId", new ObjectId(id) } });
        approvedEvents.Should().Be(1);
        var history = await api.Db.Raw(Collections.ProjectUpdates)
            .CountDocumentsAsync(new BsonDocument("projectId", (await ProjectOfAsync(id))!["_id"]));
        history.Should().Be(1);
    }

    [Fact]
    public async Task TheAuthorCannotApproveTheirOwnIdea_403_AndNothingIsCreated()
    {
        using var gestor = await NewUserClientAsync(api, Role.GESTOR, "Gestor Autor");
        var idea = await CreateIdeaAsync(gestor, title: $"Ideia do gestor {Unique()}");
        var id = idea.GetProperty("id").GetString()!;
        var pointsBefore = await Points(gestor);

        var response = await gestor.PostAsync($"{Base}/{id}/approve", null);
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().Should().Be("SELF_APPROVAL_FORBIDDEN");
        (await GetIdeaAsync(gestor, id)).GetProperty("status").GetString().Should().Be("SUBMETIDA");
        (await ProjectCountAsync(id)).Should().Be(0);
        (await Points(gestor)).Should().Be(pointsBefore);
    }

    [Fact]
    public async Task AnotherGestor_CanApprove_AGestorsIdea()
    {
        using var author = await NewUserClientAsync(api, Role.GESTOR, "Gestor Autor");
        using var other = api.ClientAs(Role.GESTOR);
        var id = (await CreateIdeaAsync(author)).GetProperty("id").GetString()!;

        (await other.PostAsync($"{Base}/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Points(author)).Should().Be(10 + 50);
    }

    [Theory]
    [InlineData(Role.OPERADOR, HttpStatusCode.Forbidden)]
    [InlineData(Role.LIDER, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Approve_OnlyGestor(Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);
        (await client.PostAsync($"{Base}/665f00000000000000000abc/approve", null)).StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("665f00000000000000000abc")]
    [InlineData("nao-e-objectid")]
    public async Task Approve_UnknownOrMalformedId_Returns404(string id)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.PostAsync($"{Base}/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =============================== JORNADA COMPLETA ===============================

    [Fact]
    public async Task FullJourney_Create_Ice_Approve_TheAuthorSeesEverything()
    {
        using var op = await NewUserClientAsync(api, name: "Operadora da Jornada");
        using var gestor = api.ClientAs(Role.GESTOR);
        var guidelineId = await AnyGuidelineIdAsync(api);
        var id = (await CreateIdeaAsync(op, guidelineId, "Ideia da jornada")).GetProperty("id").GetString()!;
        (await GetIdeaAsync(op, id)).GetProperty("status").GetString().Should().Be("SUBMETIDA");

        await gestor.PutAsJsonAsync($"{Base}/{id}/ice", new { impact = 9, confidence = 8, ease = 7 });
        (await GetIdeaAsync(op, id)).GetProperty("status").GetString().Should().Be("EM_ANALISE");

        var approve = await Json(await gestor.PostAsync($"{Base}/{id}/approve", null));

        var seen = await GetIdeaAsync(op, id);
        seen.GetProperty("status").GetString().Should().Be("APROVADA");
        seen.GetProperty("ice").GetProperty("score").GetInt32().Should().Be(504);
        seen.GetProperty("reviewedAt").ValueKind.Should().Be(JsonValueKind.String);
        var linked = seen.GetProperty("linkedProject");
        linked.GetProperty("id").GetString().Should().Be(approve.GetProperty("projectId").GetString());
        linked.GetProperty("stage").GetString().Should().Be("PLANEJAMENTO");

        var me = await Me(op);
        me.GetProperty("points").GetInt32().Should().Be(65);
        me.GetProperty("badges").EnumerateArray().Select(b => b.GetString()).Should().BeEquivalentTo(["Primeira Ideia", "Estrategista"]);

        var ranking = (await Json(await op.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray().ToList();
        ranking.Single(r => r.GetProperty("id").GetString() == me.GetProperty("id").GetString()).GetProperty("monthPoints").GetInt32().Should().Be(65);

        // aprovada: o autor não edita nem exclui mais
        (await op.PutAsJsonAsync($"{Base}/{id}", new { title = "Tarde demais", category = "tecnologia" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await op.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SeededApprovedIdeas_AlreadyHaveTheirDraftProjects_AndReapprovingIsANoOp()
    {
        var approved = await SeededIdeaIdAsync("APROVADA");
        using var gestor = api.ClientAs(Role.GESTOR);

        var response = await Json(await gestor.PostAsync($"{Base}/{approved}/approve", null));

        response.GetProperty("alreadyApproved").GetBoolean().Should().BeTrue();
        response.GetProperty("projectId").GetString().Should().Be((await ProjectOfAsync(approved))!["_id"].AsObjectId.ToString());
        (await ProjectCountAsync(approved)).Should().Be(1);
    }
}
