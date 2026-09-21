using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Api.Tests.Guidelines;

/// <summary>
/// Cada teste cria as próprias orientações (campanhas únicas) sobre o mesmo banco semeado — sem depender de ordem
/// nem de contagens absolutas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GuidelinesApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private const string Base = "/api/v1/guidelines";

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<JsonElement> CreateAsync(string? campaign = null, Pillar pillar = Pillar.IDEIAS, string? title = null)
    {
        using var lider = api.ClientAs(Role.LIDER);
        var response = await lider.PostAsJsonAsync(Base, new { title = title ?? $"Orientação {Unique()}", description = "Descrição", pillar = pillar.ToString(), campaign });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await Json(response);
    }

    /// <summary>ISO-8601 com "Z" → UTC (DateTime.Parse converteria para o fuso local).</summary>
    private static DateTime Utc(string? iso) => DateTimeOffset.Parse(iso!).UtcDateTime;

    private static StringContent Raw(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- criação ----------

    [Fact]
    public async Task Lider_Creates_With201_Location_AndAuthorFromTheToken()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var me = await Json(await lider.GetAsync("/api/v1/auth/me"));

        // authorId/createdAt no corpo são ignorados (sem mass assignment)
        var response = await lider.PostAsync(Base, Raw($$"""
            {"title":"Nova estratégia {{Unique()}}","description":"d","pillar":"DIRECIONAMENTO","campaign":"Campanha Nova",
             "authorId":"665f00000000000000000999","authorName":"Impostor","createdAt":"2001-01-01T00:00:00Z"}
            """));
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().Be($"{Base}/{body.GetProperty("id").GetString()}");
        body.GetProperty("authorId").GetString().Should().Be(me.GetProperty("id").GetString());
        body.GetProperty("authorName").GetString().Should().Be("Líder INOVAGAB");
        body.GetProperty("pillar").GetString().Should().Be("DIRECIONAMENTO");
        body.GetProperty("campaign").GetString().Should().Be("Campanha Nova");
        Utc(body.GetProperty("createdAt").GetString()).Year.Should().BeGreaterThan(2020);
    }

    [Fact]
    public async Task Created_AppearsAtTheTopOfTheList_AndCanBeFetchedById()
    {
        var created = await CreateAsync();
        var id = created.GetProperty("id").GetString()!;
        using var operador = api.ClientAs(Role.OPERADOR);

        var list = await Json(await operador.GetAsync(Base));
        var detail = await operador.GetAsync($"{Base}/{id}");

        list.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(id);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(detail)).GetProperty("title").GetString().Should().Be(created.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Create_RecordsAHistoryEntry_WithIdDateCategoryAndCampaign()
    {
        var campaign = $"Campanha {Unique()}";
        var created = await CreateAsync(campaign, Pillar.MENSURACAO);
        using var gestor = api.ClientAs(Role.GESTOR);

        var history = await Json(await gestor.GetAsync($"{Base}/history?guidelineId={created.GetProperty("id").GetString()}"));

        var entry = history.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        entry.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        entry.GetProperty("action").GetString().Should().Be("CREATED");
        entry.GetProperty("category").GetString().Should().Be("MENSURACAO");
        entry.GetProperty("campaign").GetString().Should().Be(campaign);
        Utc(entry.GetProperty("occurredAt").GetString()).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        entry.GetProperty("snapshot").GetProperty("title").GetString().Should().Be(created.GetProperty("title").GetString());
    }

    // ---------- permissões ----------

    public static IEnumerable<object[]> WriteCases()
    {
        foreach (var role in new[] { Role.OPERADOR, Role.GESTOR })
            foreach (var verb in new[] { "POST", "PUT", "DELETE" })
                yield return [role, verb];
    }

    [Theory]
    [MemberData(nameof(WriteCases))]
    public async Task NonLider_CannotWrite_403(Role role, string verb)
    {
        var target = await CreateAsync();
        var id = target.GetProperty("id").GetString();
        using var client = api.ClientAs(role);
        var payload = JsonContent.Create(new { title = "Tentativa de escrita", description = "d", pillar = "IDEIAS" });

        var response = verb switch
        {
            "POST" => await client.PostAsync(Base, payload),
            "PUT" => await client.PutAsync($"{Base}/{id}", payload),
            _ => await client.DeleteAsync($"{Base}/{id}")
        };

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(response)).GetProperty("code").GetString().Should().Be("FORBIDDEN");
        // nada foi alterado
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/history")]
    [InlineData("POST", "")]
    public async Task Anonymous_Gets401(string verb, string suffix)
    {
        using var anon = api.ClientAs((Role?)null);
        var response = verb == "GET"
            ? await anon.GetAsync(Base + suffix)
            : await anon.PostAsJsonAsync(Base + suffix, new { title = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(Role.OPERADOR)]
    [InlineData(Role.GESTOR)]
    [InlineData(Role.LIDER)]
    public async Task EveryRole_CanRead_ListDetailAndHistory(Role role)
    {
        var created = await CreateAsync();
        var id = created.GetProperty("id").GetString();
        using var client = api.ClientAs(role);

        (await client.GetAsync(Base)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"{Base}/history")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- edição ----------

    [Fact]
    public async Task Update_ChangesFields_MovesToTheTop_AndRecordsUpdatedHistory()
    {
        var target = await CreateAsync($"Antiga {Unique()}");
        var id = target.GetProperty("id").GetString()!;
        await CreateAsync();                         // uma mais nova por cima
        await Task.Delay(20);
        using var lider = api.ClientAs(Role.LIDER);

        var newCampaign = $"Nova {Unique()}";
        var response = await lider.PutAsJsonAsync($"{Base}/{id}", new { title = "Título revisado", description = "Outra", pillar = "PROJETOS", campaign = newCampaign });
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (body.GetProperty("title").GetString(), body.GetProperty("pillar").GetString(), body.GetProperty("campaign").GetString())
            .Should().Be(("Título revisado", "PROJETOS", newCampaign));
        body.GetProperty("createdAt").GetString().Should().Be(target.GetProperty("createdAt").GetString());
        body.GetProperty("authorId").GetString().Should().Be(target.GetProperty("authorId").GetString());
        Utc(body.GetProperty("updatedAt").GetString()).Should().BeAfter(Utc(target.GetProperty("updatedAt").GetString()));

        (await Json(await lider.GetAsync(Base))).GetProperty("items")[0].GetProperty("id").GetString().Should().Be(id, "editar reposiciona no topo");

        var history = (await Json(await lider.GetAsync($"{Base}/history?guidelineId={id}"))).GetProperty("items").EnumerateArray().ToList();
        history.Select(h => h.GetProperty("action").GetString()).Should().Equal("UPDATED", "CREATED");
        history[0].GetProperty("campaign").GetString().Should().Be(newCampaign);
        history[0].GetProperty("category").GetString().Should().Be("PROJETOS");
        history[1].GetProperty("snapshot").GetProperty("title").GetString().Should().Be(target.GetProperty("title").GetString());
    }

    // ---------- exclusão ----------

    [Fact]
    public async Task Delete_Returns204_Then404_AndTheHistorySurvives()
    {
        var target = await CreateAsync($"Efêmera {Unique()}", Pillar.PROJETOS);
        var id = target.GetProperty("id").GetString()!;
        using var lider = api.ClientAs(Role.LIDER);

        var deleted = await lider.DeleteAsync($"{Base}/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await lider.GetAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lider.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Json(await lider.GetAsync(Base))).GetProperty("items").EnumerateArray()
            .Should().NotContain(g => g.GetProperty("id").GetString() == id);

        var history = (await Json(await lider.GetAsync($"{Base}/history?guidelineId={id}"))).GetProperty("items").EnumerateArray().ToList();
        history.Select(h => h.GetProperty("action").GetString()).Should().Equal("DELETED", "CREATED");
        history[0].GetProperty("title").GetString().Should().Be(target.GetProperty("title").GetString());
        history[0].GetProperty("category").GetString().Should().Be("PROJETOS");
    }

    [Fact]
    public async Task DeletingAGuidelineWithLinkedIdeas_LeavesTheIdeasIntact()
    {
        var target = await CreateAsync();
        var id = target.GetProperty("id").GetString()!;

        Idea idea;
        await using (var ctx = api.Db.CreateContext())
        {
            var author = await ctx.Users.AsNoTracking().FirstAsync(u => u.Role == Role.OPERADOR);
            idea = Idea.Create("Ideia vinculada à orientação", "d", "tecnologia", Division.LOGISTICA, id, author.Id, author.Name, DateTime.UtcNow);
            ctx.Ideas.Add(idea);
            await ctx.SaveChangesAsync();
        }

        using var lider = api.ClientAs(Role.LIDER);
        (await lider.DeleteAsync($"{Base}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var raw = await api.Db.Raw(Collections.Ideas).Find(new BsonDocument("_id", new ObjectId(idea.Id))).SingleAsync();
        raw["guidelineId"].AsObjectId.ToString().Should().Be(id, "o vínculo fica órfão, mas a ideia é preservada (R2-02.6)");
        raw["status"].AsString.Should().Be("SUBMETIDA");
    }

    // ---------- validação ----------

    public static IEnumerable<object[]> InvalidBodies()
    {
        yield return ["""{"pillar":"IDEIAS"}""", "title"];
        yield return ["""{"title":"ab","pillar":"IDEIAS"}""", "title"];
        yield return [$$"""{"title":"{{new string('x', 121)}}","pillar":"IDEIAS"}""", "title"];
        yield return ["""{"title":"Título válido"}""", "pillar"];
        yield return [$$"""{"title":"Título válido","description":"{{new string('d', 2001)}}","pillar":"IDEIAS"}""", "description"];
        yield return [$$"""{"title":"Título válido","pillar":"IDEIAS","campaign":"{{new string('c', 81)}}"}""", "campaign"];
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidBody_Returns400_WithTheOffendingField(string json, string field)
    {
        using var lider = api.ClientAs(Role.LIDER);

        var response = await lider.PostAsync(Base, Raw(json));
        var body = await Json(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        body.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task UnknownPillarValue_Returns400()
    {
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.PostAsync(Base, Raw("""{"title":"Título válido","pillar":"INEXISTENTE"}"""))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidUpdate_LeavesTheGuidelineAndHistoryUntouched()
    {
        var target = await CreateAsync();
        var id = target.GetProperty("id").GetString();
        using var lider = api.ClientAs(Role.LIDER);

        (await lider.PutAsync($"{Base}/{id}", Raw("""{"title":"x","pillar":"IDEIAS"}"""))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Json(await lider.GetAsync($"{Base}/{id}"))).GetProperty("title").GetString().Should().Be(target.GetProperty("title").GetString());
        (await Json(await lider.GetAsync($"{Base}/history?guidelineId={id}"))).GetProperty("totalItems").GetInt32().Should().Be(1);
    }

    // ---------- ids ----------

    [Theory]
    [InlineData("nao-e-objectid")]
    [InlineData("123")]
    [InlineData("665f00000000000000000abc")]   // formato válido, inexistente
    public async Task UnknownOrMalformedIds_Return404_ForGetPutAndDelete(string id)
    {
        using var lider = api.ClientAs(Role.LIDER);
        var body = JsonContent.Create(new { title = "Título válido", pillar = "IDEIAS" });

        foreach (var response in new[] { await lider.GetAsync($"{Base}/{id}"), await lider.PutAsync($"{Base}/{id}", body), await lider.DeleteAsync($"{Base}/{id}") })
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- histórico: filtros e paginação ----------

    [Fact]
    public async Task History_FiltersByCampaignCategoryAndPeriod_NewestFirst()
    {
        var campaign = $"Filtro {Unique()}";
        var a = await CreateAsync(campaign, Pillar.IDEIAS);
        var b = await CreateAsync(campaign, Pillar.PROJETOS);
        await CreateAsync($"Outra {Unique()}", Pillar.IDEIAS);
        using var lider = api.ClientAs(Role.LIDER);

        var byCampaign = (await Json(await lider.GetAsync($"{Base}/history?campaign={Uri.EscapeDataString(campaign)}"))).GetProperty("items").EnumerateArray().ToList();
        byCampaign.Select(h => h.GetProperty("guidelineId").GetString()).Should().Equal(b.GetProperty("id").GetString(), a.GetProperty("id").GetString());

        var byCategory = (await Json(await lider.GetAsync($"{Base}/history?campaign={Uri.EscapeDataString(campaign)}&category=PROJETOS"))).GetProperty("items");
        byCategory.GetArrayLength().Should().Be(1);
        byCategory[0].GetProperty("guidelineId").GetString().Should().Be(b.GetProperty("id").GetString());

        var future = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        (await Json(await lider.GetAsync($"{Base}/history?campaign={Uri.EscapeDataString(campaign)}&from={future}"))).GetProperty("totalItems").GetInt32().Should().Be(0);
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        (await Json(await lider.GetAsync($"{Base}/history?campaign={Uri.EscapeDataString(campaign)}&from={yesterday}&to={future}"))).GetProperty("totalItems").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task History_PagesTheResults()
    {
        var campaign = $"Paginação {Unique()}";
        for (var i = 0; i < 3; i++) await CreateAsync(campaign);
        using var lider = api.ClientAs(Role.LIDER);

        var page = await Json(await lider.GetAsync($"{Base}/history?campaign={Uri.EscapeDataString(campaign)}&page=2&pageSize=2"));

        page.GetProperty("items").GetArrayLength().Should().Be(1);
        (page.GetProperty("totalItems").GetInt32(), page.GetProperty("totalPages").GetInt32(), page.GetProperty("page").GetInt32()).Should().Be((3, 2, 2));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=201")]
    [InlineData("pageSize=0")]
    [InlineData("category=NADA")]
    [InlineData("from=2026-09-02&to=2026-09-01")]
    [InlineData("from=nao-e-data")]
    public async Task History_InvalidQuery_Returns400(string query)
    {
        using var lider = api.ClientAs(Role.LIDER);
        (await lider.GetAsync($"{Base}/history?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_ForAnUnknownGuideline_IsEmpty_NotAnError()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var body = await Json(await lider.GetAsync($"{Base}/history?guidelineId=lixo"));
        body.GetProperty("totalItems").GetInt32().Should().Be(0);
    }

    // ---------- lista ----------

    [Fact]
    public async Task List_IsPaged_WithTotals_AndValidatesPaging()
    {
        await CreateAsync(); await CreateAsync();
        using var operador = api.ClientAs(Role.OPERADOR);

        var page = await Json(await operador.GetAsync($"{Base}?page=1&pageSize=1"));

        page.GetProperty("items").GetArrayLength().Should().Be(1);
        page.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(6, "4 do seed + as criadas nos testes");
        page.GetProperty("totalPages").GetInt32().Should().BeGreaterThanOrEqualTo(6);
        (await operador.GetAsync($"{Base}?pageSize=500")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await operador.GetAsync($"{Base}?page=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SeededGuidelines_AreListed_WithTheirCampaigns()
    {
        using var operador = api.ClientAs(Role.OPERADOR);
        var items = (await Json(await operador.GetAsync($"{Base}?pageSize=200"))).GetProperty("items").EnumerateArray().ToList();

        items.Select(g => g.GetProperty("title").GetString()).Should().Contain(
            ["Eficiência operacional na logística", "Experiência do passageiro", "Sustentabilidade e redução de custos", "Mensuração do retorno da inovação"]);
        items.Single(g => g.GetProperty("title").GetString() == "Experiência do passageiro").GetProperty("campaign").GetString().Should().Be("Campanha Cliente 2026");
    }

    [Fact]
    public async Task SeededHistory_KeepsTheOriginalAndTheEditedSnapshot()
    {
        using var lider = api.ClientAs(Role.LIDER);
        var guideline = (await Json(await lider.GetAsync($"{Base}?pageSize=200"))).GetProperty("items").EnumerateArray()
            .Single(g => g.GetProperty("title").GetString() == "Experiência do passageiro");

        var history = (await Json(await lider.GetAsync($"{Base}/history?guidelineId={guideline.GetProperty("id").GetString()}"))).GetProperty("items").EnumerateArray().ToList();

        history.Select(h => h.GetProperty("action").GetString()).Should().Equal("UPDATED", "CREATED");
        history[0].GetProperty("snapshot").GetProperty("description").GetString().Should().Contain("Prioridade: terminais rodoviários");
        history[1].GetProperty("snapshot").GetProperty("description").GetString().Should().NotContain("Prioridade");
    }
}
