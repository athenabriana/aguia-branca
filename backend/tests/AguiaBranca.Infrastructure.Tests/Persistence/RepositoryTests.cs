using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Repositories;
using AguiaBranca.Infrastructure.Tests.Support;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Persistence;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RepositoryTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private static readonly PageRequest All = new(1, 200);

    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SaveAsync(Action<AppDbContext> seed)
    {
        await using var ctx = _db.CreateContext();
        seed(ctx);
        await ctx.SaveChangesAsync();
    }

    // ---------- Ideas ----------

    [Fact]
    public async Task Ideas_Query_FiltersByAuthorStatusGuidelineDivision()
    {
        var ana = Builders.User(name: "Ana"); var bia = Builders.User(name: "Bia");
        var g1 = EntityId.New();
        var a1 = Builders.Idea(ana, g1); var a2 = Builders.Idea(ana, division: Division.COMERCIO);
        var b1 = Builders.Idea(bia, g1);
        a2.SaveIce(new Ice(5, 5, 5), ana.Id, Builders.Now); // EM_ANALISE
        await SaveAsync(c => c.Ideas.AddRange(a1, a2, b1));

        await using var ctx = _db.CreateContext();
        var repo = new IdeaRepository(ctx);

        (await repo.QueryAsync(new IdeaQuery(AuthorId: ana.Id), All, default)).TotalItems.Should().Be(2);
        (await repo.QueryAsync(new IdeaQuery(GuidelineId: g1), All, default)).TotalItems.Should().Be(2);
        (await repo.QueryAsync(new IdeaQuery(Division: Division.COMERCIO), All, default)).Items.Should().ContainSingle(i => i.Id == a2.Id);
        (await repo.QueryAsync(new IdeaQuery(Statuses: [IdeaStatus.EM_ANALISE]), All, default)).Items.Should().ContainSingle(i => i.Id == a2.Id);
        (await repo.QueryAsync(new IdeaQuery(Statuses: [IdeaStatus.SUBMETIDA, IdeaStatus.EM_ANALISE]), All, default)).TotalItems.Should().Be(3);
        (await repo.QueryAsync(new IdeaQuery(Statuses: [IdeaStatus.APROVADA]), All, default)).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Ideas_Query_IceScoreDesc_PutsUnscoredLast()
    {
        var author = Builders.User();
        var low = Builders.Idea(author, createdAt: Builders.Now.AddDays(-3), title: "Ideia baixa");
        var high = Builders.Idea(author, createdAt: Builders.Now.AddDays(-2), title: "Ideia alta");
        var none = Builders.Idea(author, createdAt: Builders.Now.AddDays(-1), title: "Ideia sem ICE");
        low.SaveIce(new Ice(2, 2, 2), author.Id, Builders.Now);
        high.SaveIce(new Ice(9, 9, 9), author.Id, Builders.Now);
        await SaveAsync(c => c.Ideas.AddRange(low, none, high));

        await using var ctx = _db.CreateContext();
        var result = await new IdeaRepository(ctx).QueryAsync(new IdeaQuery(Sort: IdeaSort.ICE_SCORE_DESC), All, default);

        result.Items.Select(i => i.Title).Should().Equal("Ideia alta", "Ideia baixa", "Ideia sem ICE");
    }

    [Fact]
    public async Task Ideas_Query_DefaultSort_IsNewestFirst_AndPagesCorrectly()
    {
        var author = Builders.User();
        var ideas = Enumerable.Range(0, 25)
            .Select(i => Builders.Idea(author, createdAt: Builders.Now.AddMinutes(i), title: $"Ideia {i:00}")).ToList();
        await SaveAsync(c => c.Ideas.AddRange(ideas));

        await using var ctx = _db.CreateContext();
        var repo = new IdeaRepository(ctx);
        var page2 = await repo.QueryAsync(new IdeaQuery(), new PageRequest(2, 10), default);

        page2.TotalItems.Should().Be(25);
        page2.TotalPages.Should().Be(3);
        page2.Items.Select(i => i.Title).Should().Equal(Enumerable.Range(5, 10).Reverse().Select(i => $"Ideia {i:00}"));
        (await repo.QueryAsync(new IdeaQuery(), new PageRequest(3, 10), default)).Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Ideas_ListByAuthor_And_ListAll_ByDivision()
    {
        var ana = Builders.User(); var bia = Builders.User();
        await SaveAsync(c => c.Ideas.AddRange(
            Builders.Idea(ana), Builders.Idea(ana), Builders.Idea(bia, division: Division.PASSAGEIROS)));

        await using var ctx = _db.CreateContext();
        var repo = new IdeaRepository(ctx);
        (await repo.ListByAuthorAsync(ana.Id, default)).Should().HaveCount(2);
        (await repo.ListAllAsync(null, default)).Should().HaveCount(3);
        (await repo.ListAllAsync(Division.PASSAGEIROS, default)).Should().ContainSingle();
    }

    [Fact]
    public async Task InvalidIds_NeverReachTheDatabase_AndBehaveAsNotFound()
    {
        await using var ctx = _db.CreateContext();
        (await new IdeaRepository(ctx).GetByIdAsync("nao-e-objectid", default)).Should().BeNull();
        (await new ProjectRepository(ctx).GetByIdAsync("123", default)).Should().BeNull();
        (await new UserRepository(ctx).GetByIdAsync("", default)).Should().BeNull();
        (await new GuidelineRepository(ctx).ExistsAsync("zzz", default)).Should().BeFalse();
        (await new IdeaRepository(ctx).QueryAsync(new IdeaQuery(AuthorId: "lixo"), All, default)).TotalItems.Should().Be(0);
        (await new ProjectRepository(ctx).GetByOriginatingIdeaIdAsync("lixo", default)).Should().BeNull();
    }

    // ---------- Projects ----------

    [Fact]
    public async Task Projects_Query_FiltersAndOrdersByUpdatedAtDesc()
    {
        var g = EntityId.New();
        var older = Builders.Project(Builders.ProjectData(ProjectStage.EM_EXECUCAO, guidelineId: g), now: Builders.Now.AddDays(-2));
        var newer = Builders.Project(Builders.ProjectData(ProjectStage.EM_EXECUCAO, guidelineId: g), now: Builders.Now);
        var other = Builders.Project(Builders.ProjectData(ProjectStage.CONCLUIDO, division: Division.COMERCIO), now: Builders.Now.AddDays(-1));
        await SaveAsync(c => c.Projects.AddRange(older, other, newer));

        await using var ctx = _db.CreateContext();
        var repo = new ProjectRepository(ctx);

        (await repo.QueryAsync(new ProjectQuery(), All, default)).Items.Select(p => p.Id).Should().Equal(newer.Id, other.Id, older.Id);
        (await repo.QueryAsync(new ProjectQuery(Stage: ProjectStage.EM_EXECUCAO), All, default)).TotalItems.Should().Be(2);
        (await repo.QueryAsync(new ProjectQuery(Division: Division.COMERCIO), All, default)).Items.Should().ContainSingle(p => p.Id == other.Id);
        (await repo.QueryAsync(new ProjectQuery(GuidelineId: g), All, default)).TotalItems.Should().Be(2);
        (await repo.ListAllAsync(Division.LOGISTICA, default)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Projects_ByOriginatingIdea_SingleAndBatch()
    {
        var ideaA = Builders.Idea(); var ideaB = Builders.Idea(); var ideaC = Builders.Idea();
        var pa = Project.CreateDraftFromIdea(ideaA, EntityId.New(), "G", Builders.Now);
        var pb = Project.CreateDraftFromIdea(ideaB, EntityId.New(), "G", Builders.Now);
        await SaveAsync(c => c.Projects.AddRange(pa, pb, Builders.Project()));

        await using var ctx = _db.CreateContext();
        var repo = new ProjectRepository(ctx);

        (await repo.GetByOriginatingIdeaIdAsync(ideaA.Id, default))!.Id.Should().Be(pa.Id);
        (await repo.GetByOriginatingIdeaIdAsync(ideaC.Id, default)).Should().BeNull();
        var batch = await repo.GetByOriginatingIdeaIdsAsync([ideaA.Id, ideaB.Id, ideaC.Id, "invalido"], default);
        batch.Select(p => p.Id).Should().BeEquivalentTo([pa.Id, pb.Id]);
    }

    [Fact]
    public async Task ProjectUpdates_ListByProject_NewestFirst_AndRemoveByProject()
    {
        var projectId = EntityId.New();
        var otherProject = EntityId.New();
        ProjectUpdate U(string pid, int minutes, string note) =>
            ProjectUpdate.Create(pid, EntityId.New(), "G", note, [], Builders.Now.AddMinutes(minutes));
        await SaveAsync(c => c.ProjectUpdates.AddRange(U(projectId, 0, "criado"), U(projectId, 10, "editado"), U(otherProject, 5, "outro")));

        await using (var ctx = _db.CreateContext())
        {
            var repo = new ProjectUpdateRepository(ctx);
            var page = await repo.ListByProjectAsync(projectId, All, default);
            page.Items.Select(u => u.Note).Should().Equal("editado", "criado");
            page.TotalItems.Should().Be(2);

            await repo.RemoveByProjectAsync(projectId, default);
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var rp = new ProjectUpdateRepository(read);
        (await rp.ListByProjectAsync(projectId, All, default)).TotalItems.Should().Be(0);
        (await rp.ListByProjectAsync(otherProject, All, default)).TotalItems.Should().Be(1);
    }

    // ---------- Guidelines ----------

    [Fact]
    public async Task Guidelines_ListByUpdatedAtDesc_Titles_And_Exists()
    {
        var first = Builders.Guideline("Primeira"); var second = Builders.Guideline("Segunda");
        second.Update("Segunda editada", "d", Pillar.PROJETOS, null, Builders.Now.AddHours(1));
        await SaveAsync(c => c.Guidelines.AddRange(first, second));

        await using var ctx = _db.CreateContext();
        var repo = new GuidelineRepository(ctx);

        (await repo.ListAsync(default)).Select(g => g.Title).Should().Equal("Segunda editada", "Primeira");
        (await repo.ExistsAsync(first.Id, default)).Should().BeTrue();
        (await repo.ExistsAsync(EntityId.New(), default)).Should().BeFalse();

        var titles = await repo.GetTitlesAsync([first.Id, second.Id, EntityId.New(), "lixo"], default);
        titles.Should().BeEquivalentTo(new Dictionary<string, string> { [first.Id] = "Primeira", [second.Id] = "Segunda editada" });
    }

    [Fact]
    public async Task Guidelines_ListPaged_IsNewestFirst_WithTotals()
    {
        var items = Enumerable.Range(0, 5).Select(i =>
        {
            var g = Builders.Guideline($"Orientação {i}");
            g.Update($"Orientação {i}", "d", Pillar.IDEIAS, null, Builders.Now.AddMinutes(i));
            return g;
        }).ToList();
        await SaveAsync(c => c.Guidelines.AddRange(items));

        await using var ctx = _db.CreateContext();
        var repo = new GuidelineRepository(ctx);

        var page2 = await repo.ListPagedAsync(new PageRequest(2, 2), default);

        page2.Items.Select(g => g.Title).Should().Equal("Orientação 2", "Orientação 1");
        (page2.TotalItems, page2.TotalPages).Should().Be((5, 3));
        (await repo.ListPagedAsync(new PageRequest(3, 2), default)).Items.Should().ContainSingle();
        (await repo.ListPagedAsync(new PageRequest(9, 2), default)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GuidelineHistory_Query_FiltersAndOrdersNewestFirst()
    {
        var g = Builders.Guideline(campaign: "A", pillar: Pillar.IDEIAS);
        var h1 = GuidelineHistoryEntry.From(g, GuidelineAction.CREATED, g.AuthorId, "L", Builders.Now);
        g.Update("Editada", "d", Pillar.PROJETOS, "B", Builders.Now.AddDays(1));
        var h2 = GuidelineHistoryEntry.From(g, GuidelineAction.UPDATED, g.AuthorId, "L", Builders.Now.AddDays(1));
        var otherGuideline = Builders.Guideline("Outra", campaign: "B", pillar: Pillar.PROJETOS);
        var h3 = GuidelineHistoryEntry.From(otherGuideline, GuidelineAction.CREATED, g.AuthorId, "L", Builders.Now.AddDays(2));
        await SaveAsync(c => c.GuidelineHistory.AddRange(h1, h2, h3));

        await using var ctx = _db.CreateContext();
        var repo = new GuidelineHistoryRepository(ctx);

        (await repo.QueryAsync(new GuidelineHistoryQuery(), All, default)).Items.Select(x => x.Id).Should().Equal(h3.Id, h2.Id, h1.Id);
        (await repo.QueryAsync(new GuidelineHistoryQuery(GuidelineId: g.Id), All, default)).TotalItems.Should().Be(2);
        (await repo.QueryAsync(new GuidelineHistoryQuery(Campaign: "B"), All, default)).TotalItems.Should().Be(2);
        (await repo.QueryAsync(new GuidelineHistoryQuery(Category: Pillar.IDEIAS), All, default)).Items.Should().ContainSingle(x => x.Id == h1.Id);
        (await repo.QueryAsync(new GuidelineHistoryQuery(From: Builders.Now.AddDays(1), To: Builders.Now.AddDays(1).AddHours(1)), All, default))
            .Items.Should().ContainSingle(x => x.Id == h2.Id);
    }

    // ---------- Users / points / tokens / cache ----------

    [Fact]
    public async Task Users_ListByRole_GetByIds()
    {
        var op = Builders.User(Role.OPERADOR, "Zé"); var op2 = Builders.User(Role.OPERADOR, "Ana");
        var gestor = Builders.User(Role.GESTOR, "Gil");
        await SaveAsync(c => c.Users.AddRange(op, op2, gestor));

        await using var ctx = _db.CreateContext();
        var repo = new UserRepository(ctx);

        (await repo.ListAsync(Role.OPERADOR, default)).Select(u => u.Name).Should().Equal("Ana", "Zé");
        (await repo.ListAsync(null, default)).Should().HaveCount(3);
        (await repo.GetByIdsAsync([op.Id, gestor.Id, "lixo"], default)).Select(u => u.Id).Should().BeEquivalentTo([op.Id, gestor.Id]);
        (await repo.GetByIdAsync(op2.Id, default))!.Name.Should().Be("Ana");
    }

    [Fact]
    public async Task PointEvents_ListBetween_IsHalfOpenInterval()
    {
        var user = EntityId.New();
        PointEvent E(DateTime at) => PointEvent.Create(user, 10, PointReason.IDEA_CREATED, null, at);
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        await SaveAsync(c => c.PointEvents.AddRange(
            E(from.AddSeconds(-1)), E(from), E(to.AddSeconds(-1)), E(to)));

        await using var ctx = _db.CreateContext();
        var events = await new PointEventRepository(ctx).ListBetweenAsync(from, to, default);

        events.Should().HaveCount(2);
        events.Select(e => e.CreatedAt).Should().BeEquivalentTo([from, to.AddSeconds(-1)]);
    }

    [Fact]
    public async Task RefreshTokens_ByHash_AndFamily()
    {
        var t1 = RefreshToken.Issue(EntityId.New(), "h1", Builders.Now, TimeSpan.FromDays(7));
        var t2 = RefreshToken.Issue(t1.UserId, "h2", Builders.Now, TimeSpan.FromDays(7), t1.FamilyId);
        var other = RefreshToken.Issue(EntityId.New(), "h3", Builders.Now, TimeSpan.FromDays(7));
        await SaveAsync(c => c.RefreshTokens.AddRange(t1, t2, other));

        await using var ctx = _db.CreateContext();
        var repo = new RefreshTokenRepository(ctx);

        (await repo.GetByHashAsync("h2", default))!.Id.Should().Be(t2.Id);
        (await repo.GetByHashAsync("nope", default)).Should().BeNull();
        (await repo.ListByFamilyAsync(t1.FamilyId, default)).Select(t => t.TokenHash).Should().BeEquivalentTo(["h1", "h2"]);
    }

    [Fact]
    public async Task InsightCache_GetSetReplaceExpireCount()
    {
        var now = Builders.Now;
        var user = EntityId.New();
        CachedInsight Make(string payload, DateTime created, DateTime expires) =>
            new("key-1", user, "ALL|-", "model-x", payload, created, expires);

        await using (var ctx = _db.CreateContext())
        {
            await new InsightCache(ctx).SetAsync(Make("v1", now, now.AddHours(6)), default);
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _db.CreateContext())
        {
            var cache = new InsightCache(ctx);
            (await cache.GetAsync("key-1", now.AddHours(1), default))!.PayloadJson.Should().Be("v1");
            (await cache.GetAsync("key-1", now.AddHours(6), default)).Should().BeNull("expirada");
            (await cache.GetAsync("outra", now, default)).Should().BeNull();

            await cache.SetAsync(Make("v2", now.AddHours(7), now.AddHours(13)), default); // substitui a mesma chave
            await ctx.SaveChangesAsync();
        }
        await using var read = _db.CreateContext();
        var c2 = new InsightCache(read);
        (await c2.GetAsync("key-1", now.AddHours(8), default))!.PayloadJson.Should().Be("v2");
        (await _db.Raw(Collections.AiInsights).CountDocumentsAsync(MongoDB.Bson.BsonDocument.Parse("{}"))).Should().Be(1);
    }
}
