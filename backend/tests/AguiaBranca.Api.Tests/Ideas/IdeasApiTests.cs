using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Api.Tests.Ideas;

[Trait("Category", "Integration")]
public sealed class IdeasApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string Base = "/api/v1/ideas";

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static StringContent Raw(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<int> Points(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/auth/me"))).GetProperty("points").GetInt32();

    private static async Task<string[]> Badges(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/auth/me"))).GetProperty("badges").EnumerateArray().Select(b => b.GetString()!).ToArray();

    private async Task<string> AnyGuidelineIdAsync()
    {
        using var lider = api.ClientAs(Role.LIDER);
        return (await Json(await lider.GetAsync("/api/v1/guidelines?pageSize=1"))).GetProperty("items")[0].GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> CreateAsync(HttpClient client, string? guidelineId = null, string? title = null, string division = "LOGISTICA")
    {
        var response = await client.PostAsJsonAsync(Base, new
        {
            title = title ?? $"Ideia {Unique()}", description = "Descrição", category = "tecnologia", division, guidelineId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    /// <summary>Operador novo (sem histórico), com login feito pela API.</summary>
    private async Task<HttpClient> NewOperatorAsync()
    {
        var email = $"op-{Unique()}@aguiabranca.com";
        using (var scope = api.Factory.Services.CreateScope())
        {
            var user = AppUser.Create("Operador Novo", email, Role.OPERADOR, Division.PASSAGEIROS, DateTime.UtcNow);
            (await scope.ServiceProvider.GetRequiredService<IIdentityService>().CreateUserAsync(user, SeededApiFixture.Password, default)).IsSuccess.Should().BeTrue();
        }
        var login = await Json(await api.Anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = SeededApiFixture.Password }));
        var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }

    // ---------- criação ----------

    [Fact]
    public async Task Operator_CreatesWithGuideline_Gets201_15Points_AndTheIdeaIsSubmitted()
    {
        using var op = await NewOperatorAsync();
        var guidelineId = await AnyGuidelineIdAsync();

        var response = await op.PostAsJsonAsync(Base, new { title = "Roteirização dinâmica", description = "d", category = "tecnologia", division = "LOGISTICA", guidelineId });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().Be($"{Base}/{body.GetProperty("id").GetString()}");
        body.GetProperty("status").GetString().Should().Be("SUBMETIDA");
        body.GetProperty("pointsAwarded").GetInt32().Should().Be(15);
        body.GetProperty("guidelineTitle").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("category").GetString().Should().Be("Tecnologia");
        body.GetProperty("ice").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("linkedProject").ValueKind.Should().Be(JsonValueKind.Null);
        (await Points(op)).Should().Be(15);
    }

    [Fact]
    public async Task WithoutGuideline_Awards10()
    {
        using var op = await NewOperatorAsync();

        var body = await CreateAsync(op);

        body.GetProperty("pointsAwarded").GetInt32().Should().Be(10);
        (await Points(op)).Should().Be(10);
    }

    [Fact]
    public async Task ServerControlledFields_InTheBody_AreIgnored()
    {
        using var op = await NewOperatorAsync();
        var me = await Json(await op.GetAsync("/api/v1/auth/me"));

        var response = await op.PostAsync(Base, Raw("""
            {"title":"Tentativa de fraude","description":"d","category":"tecnologia","division":"LOGISTICA",
             "status":"IMPLEMENTADA","authorId":"665f00000000000000000999","authorName":"Impostor",
             "ice":{"impact":10,"confidence":10,"ease":10},"reviewerId":"665f00000000000000000998",
             "pointsAwarded":9999,"createdAt":"2001-01-01T00:00:00Z"}
            """));
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.GetProperty("status").GetString().Should().Be("SUBMETIDA");
        body.GetProperty("authorId").GetString().Should().Be(me.GetProperty("id").GetString());
        body.GetProperty("authorName").GetString().Should().Be("Operador Novo");
        body.GetProperty("ice").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("reviewerId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("pointsAwarded").GetInt32().Should().Be(10);
        DateTimeOffset.Parse(body.GetProperty("createdAt").GetString()!).Year.Should().BeGreaterThan(2020);
        (await Points(op)).Should().Be(10);
    }

    [Fact]
    public async Task UnknownGuideline_Returns422_AndNoPointsAreGiven()
    {
        using var op = await NewOperatorAsync();

        var response = await op.PostAsJsonAsync(Base, new { title = "Ideia válida", description = "d", category = "tecnologia", guidelineId = "665f00000000000000000abc" });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        body.GetProperty("code").GetString().Should().Be("GUIDELINE_NOT_FOUND");
        body.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("guidelineId");
        (await Points(op)).Should().Be(0);
        (await Json(await op.GetAsync($"{Base}?scope=mine"))).GetProperty("totalItems").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(Role.OPERADOR, HttpStatusCode.Created)]
    [InlineData(Role.GESTOR, HttpStatusCode.Created)]
    [InlineData(Role.LIDER, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Create_PermissionMatrix(Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);
        var response = await client.PostAsJsonAsync(Base, new { title = $"Ideia {Unique()}", description = "d", category = "tecnologia", division = "LOGISTICA" });
        response.StatusCode.Should().Be(expected);
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        yield return ["""{"category":"tecnologia"}""", "title"];
        yield return ["""{"title":"ab","category":"tecnologia"}""", "title"];
        yield return [$$"""{"title":"{{new string('x', 121)}}","category":"tecnologia"}""", "title"];
        yield return ["""{"title":"Título válido"}""", "category"];
        yield return ["""{"title":"Título válido","category":"x"}""", "category"];
        yield return [$$"""{"title":"Título válido","category":"tecnologia","description":"{{new string('d', 2001)}}"}""", "description"];
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidBody_Returns400_WithField(string json, string field)
    {
        using var op = await NewOperatorAsync();
        var response = await op.PostAsync(Base, Raw(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
        (await Points(op)).Should().Be(0);
    }

    [Fact]
    public async Task InvalidDivisionValue_Returns400()
    {
        using var op = await NewOperatorAsync();
        (await op.PostAsync(Base, Raw("""{"title":"Título válido","category":"tecnologia","division":"MARTE"}"""))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- badges ----------

    [Fact]
    public async Task FirstIdea_GrantsPrimeiraIdeia_AndFiveInTheMonthGrantInovadorDoMes()
    {
        using var op = await NewOperatorAsync();
        (await Badges(op)).Should().BeEmpty();

        await CreateAsync(op);
        (await Badges(op)).Should().Equal("Primeira Ideia");

        for (var i = 0; i < 3; i++) await CreateAsync(op);
        (await Badges(op)).Should().NotContain("Inovador do Mês");

        await CreateAsync(op);
        (await Badges(op)).Should().BeEquivalentTo(["Primeira Ideia", "Inovador do Mês"]);

        await CreateAsync(op); // a 6ª não duplica
        (await Badges(op)).Should().HaveCount(2);
    }

    // ---------- visibilidade ----------

    [Fact]
    public async Task Operator_CannotSeeAnotherOperatorsIdea_ButManagersAndLeadersCan()
    {
        using var alice = await NewOperatorAsync();
        using var bob = await NewOperatorAsync();
        var idea = await CreateAsync(alice, title: $"Segredo da Alice {Unique()}");
        var id = idea.GetProperty("id").GetString();

        (await bob.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var bobsList = await Json(await bob.GetAsync($"{Base}?scope=all&pageSize=200"));
        bobsList.GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetString() == id);

        foreach (var role in new[] { Role.GESTOR, Role.LIDER })
        {
            using var client = api.ClientAs(role);
            (await client.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task List_Scopes_MineAllAndCuration()
    {
        using var op = await NewOperatorAsync();
        await CreateAsync(op, title: $"Minha {Unique()}");
        using var gestor = api.ClientAs(Role.GESTOR);

        // operador: só as próprias, qualquer escopo
        (await Json(await op.GetAsync(Base))).GetProperty("totalItems").GetInt32().Should().Be(1);
        (await Json(await op.GetAsync($"{Base}?scope=all"))).GetProperty("totalItems").GetInt32().Should().Be(1);

        // gestor: por padrão todas; mine = só as dele
        var all = await Json(await gestor.GetAsync($"{Base}?pageSize=200"));
        all.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(7, "6 do seed + as criadas pelos testes");
        var authors = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("authorName").GetString()).Distinct().ToList();
        authors.Should().Contain(["Operador INOVAGAB", "Ana Operadora", "Bruno Operador"]);

        var mine = await Json(await gestor.GetAsync($"{Base}?scope=mine"));
        mine.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("authorName").GetString() == "Gestor INOVAGAB");
    }

    [Fact]
    public async Task Curation_ListsOpenIdeas_ByIceScoreDescending_UnscoredLast()
    {
        using var gestor = api.ClientAs(Role.GESTOR);

        var items = (await Json(await gestor.GetAsync($"{Base}?scope=curation&pageSize=200"))).GetProperty("items").EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => new[] { "SUBMETIDA", "EM_ANALISE" }.Contains(i.GetProperty("status").GetString()));
        items[0].GetProperty("title").GetString().Should().Be("Checklist digital de manutenção da frota");
        items[0].GetProperty("ice").GetProperty("score").GetInt32().Should().Be(336);
        var scores = items.Select(i => i.GetProperty("ice").ValueKind == JsonValueKind.Null ? -1 : i.GetProperty("ice").GetProperty("score").GetInt32()).ToList();
        scores.Should().BeInDescendingOrder("ICE desc, sem ICE ao fim");
    }

    [Fact]
    public async Task List_Filters_ByStatusGuidelineDivision_AndPaging()
    {
        using var gestor = api.ClientAs(Role.GESTOR);

        var approved = (await Json(await gestor.GetAsync($"{Base}?status=APROVADA&pageSize=200"))).GetProperty("items").EnumerateArray().ToList();
        approved.Should().OnlyContain(i => i.GetProperty("status").GetString() == "APROVADA");
        approved.Select(i => i.GetProperty("title").GetString()).Should().Contain(["Check-in por QR Code nos terminais", "Coleta seletiva integrada nos terminais"]);
        approved.Should().OnlyContain(i => i.GetProperty("linkedProject").ValueKind == JsonValueKind.Object, "ideias aprovadas têm projeto rascunho");

        var comercio = (await Json(await gestor.GetAsync($"{Base}?division=COMERCIO&pageSize=200"))).GetProperty("items").EnumerateArray();
        comercio.Should().OnlyContain(i => i.GetProperty("division").GetString() == "COMERCIO").And.NotBeEmpty();

        var g = approved[0].GetProperty("guidelineId").GetString();
        (await Json(await gestor.GetAsync($"{Base}?guidelineId={g}&pageSize=200"))).GetProperty("items").EnumerateArray()
            .Should().OnlyContain(i => i.GetProperty("guidelineId").GetString() == g);

        var page = await Json(await gestor.GetAsync($"{Base}?page=2&pageSize=2"));
        page.GetProperty("items").GetArrayLength().Should().Be(2);
        page.GetProperty("page").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData("scope=xyz")]
    [InlineData("status=NADA")]
    [InlineData("division=MARTE")]
    [InlineData("page=0")]
    [InlineData("pageSize=201")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.GetAsync($"{Base}?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Detail_OfAnApprovedIdea_ShowsTheLinkedProjectAndGuidelineTitle_ToItsAuthorToo()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var qr = (await Json(await gestor.GetAsync($"{Base}?status=APROVADA&pageSize=200"))).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("title").GetString() == "Check-in por QR Code nos terminais");

        qr.GetProperty("guidelineTitle").GetString().Should().Be("Experiência do passageiro");
        qr.GetProperty("linkedProject").GetProperty("stage").GetString().Should().Be("PLANEJAMENTO");
        qr.GetProperty("linkedProject").GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    // ---------- edição ----------

    [Fact]
    public async Task Author_EditsOwnSubmittedIdea_PointsUnchanged()
    {
        using var op = await NewOperatorAsync();
        var idea = await CreateAsync(op);
        var id = idea.GetProperty("id").GetString();
        var guidelineId = await AnyGuidelineIdAsync();

        var response = await op.PutAsJsonAsync($"{Base}/{id}", new { title = "Título revisado", description = "Nova", category = "operações", division = "COMERCIO", guidelineId });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (body.GetProperty("title").GetString(), body.GetProperty("category").GetString(), body.GetProperty("division").GetString())
            .Should().Be(("Título revisado", "Operações", "COMERCIO"));
        body.GetProperty("guidelineTitle").GetString().Should().NotBeNullOrEmpty();
        (await Points(op)).Should().Be(10, "editar não altera pontos (o bônus de vínculo é dado só na criação)");
    }

    [Fact]
    public async Task Edit_AfterTheReviewStarted_Returns409()
    {
        // "Checklist digital..." (operador@) está EM_ANALISE no seed
        using var op = api.ClientAs(Role.OPERADOR);
        var mine = (await Json(await op.GetAsync($"{Base}?status=EM_ANALISE"))).GetProperty("items")[0];

        var response = await op.PutAsJsonAsync($"{Base}/{mine.GetProperty("id").GetString()}", new { title = "Tentativa tardia", category = "tecnologia" });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().Should().Be("IDEA_NOT_EDITABLE");
    }

    [Fact]
    public async Task Edit_ByAnotherOperator_Is404_ByAManager_Is403()
    {
        using var alice = await NewOperatorAsync();
        using var bob = await NewOperatorAsync();
        var id = (await CreateAsync(alice)).GetProperty("id").GetString();
        var payload = new { title = "Tentativa alheia", category = "tecnologia" };

        (await bob.PutAsJsonAsync($"{Base}/{id}", payload)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.PutAsJsonAsync($"{Base}/{id}", payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.PutAsJsonAsync($"{Base}/{id}", payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- exclusão ----------

    [Theory]
    [InlineData(true, 15)]
    [InlineData(false, 10)]
    public async Task Author_Deletes_AndThePointsAreRefunded(bool withGuideline, int created)
    {
        using var op = await NewOperatorAsync();
        var keeper = await CreateAsync(op);                                   // +10 fica
        var idea = await CreateAsync(op, withGuideline ? await AnyGuidelineIdAsync() : null);
        var id = idea.GetProperty("id").GetString();
        idea.GetProperty("pointsAwarded").GetInt32().Should().Be(created);
        (await Points(op)).Should().Be(10 + created);

        var deleted = await op.DeleteAsync($"{Base}/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Points(op)).Should().Be(10, "estorno de −10/−15");
        (await op.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await op.GetAsync($"{Base}/{keeper.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_NeverLeavesPointsNegative()
    {
        using var op = await NewOperatorAsync();
        var id = (await CreateAsync(op)).GetProperty("id").GetString();
        (await op.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Points(op)).Should().Be(0);
    }

    [Fact]
    public async Task Delete_AfterTheReviewStarted_Returns409_AndTheIdeaStays()
    {
        using var op = api.ClientAs(Role.OPERADOR);
        var underReview = (await Json(await op.GetAsync($"{Base}?status=EM_ANALISE"))).GetProperty("items")[0].GetProperty("id").GetString();
        var before = await Points(op);

        var response = await op.DeleteAsync($"{Base}/{underReview}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("code").GetString().Should().Be("IDEA_NOT_EDITABLE");
        (await op.GetAsync($"{Base}/{underReview}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Points(op)).Should().Be(before);
    }

    [Fact]
    public async Task Delete_ByAnotherOperatorOrManager_IsRejected()
    {
        using var alice = await NewOperatorAsync();
        using var bob = await NewOperatorAsync();
        var id = (await CreateAsync(alice)).GetProperty("id").GetString();

        (await bob.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await alice.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- ids / órfãs ----------

    [Theory]
    [InlineData("nao-e-objectid")]
    [InlineData("665f00000000000000000abc")]
    public async Task UnknownOrMalformedIds_Return404(string id)
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        var body = JsonContent.Create(new { title = "Título válido", category = "tecnologia" });

        (await gestor.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.PutAsync($"{Base}/{id}", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IdeaOfADeletedGuideline_KeepsTheId_WithANullTitle()
    {
        using var lider = api.ClientAs(Role.LIDER);
        using var op = await NewOperatorAsync();
        var guideline = await Json(await lider.PostAsJsonAsync("/api/v1/guidelines", new { title = $"Efêmera {Unique()}", pillar = "IDEIAS" }));
        var gid = guideline.GetProperty("id").GetString()!;
        var id = (await CreateAsync(op, gid)).GetProperty("id").GetString();

        (await lider.DeleteAsync($"/api/v1/guidelines/{gid}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await Json(await op.GetAsync($"{Base}/{id}"));
        detail.GetProperty("guidelineId").GetString().Should().Be(gid);
        detail.GetProperty("guidelineTitle").ValueKind.Should().Be(JsonValueKind.Null, "a UI mostra 'Orientação removida'");
    }

    [Fact]
    public async Task Anonymous_IsRejected_OnEveryIdeaRoute()
    {
        using var anon = api.ClientAs((Role?)null);
        var id = "665f00000000000000000abc";
        (await anon.GetAsync(Base)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PutAsJsonAsync($"{Base}/{id}", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
