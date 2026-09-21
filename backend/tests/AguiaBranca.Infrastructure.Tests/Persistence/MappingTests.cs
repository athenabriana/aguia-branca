using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Persistence;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MappingTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();


    [Fact]
    public async Task Idea_RoundTrip_WithIceAndReferences_UsesNativeTypesAndCamelCase()
    {
        var guideline = Builders.Guideline();
        var idea = Builders.Idea(guidelineId: guideline.Id);
        idea.SaveIce(new Ice(8, 7, 6), guideline.Id, Builders.Now);
        await using (var ctx = _db.CreateContext()) { ctx.Ideas.Add(idea); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.Ideas.FirstAsync(x => x.Id == idea.Id);
        loaded.Ice!.Score.Should().Be(336);
        loaded.Status.Should().Be(IdeaStatus.EM_ANALISE);
        loaded.GuidelineId.Should().Be(guideline.Id);
        loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        loaded.CreatedAt.Should().Be(Builders.Now);

        var raw = await _db.Raw(Collections.Ideas).Find(new BsonDocument()).SingleAsync();
        raw["_id"].BsonType.Should().Be(BsonType.ObjectId);
        raw["authorId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["guidelineId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["status"].AsString.Should().Be("EM_ANALISE");
        raw["division"].AsString.Should().Be("LOGISTICA");
        raw["ice"]["score"].AsInt32.Should().Be(336);
        raw["ice"]["impact"].AsInt32.Should().Be(8);
        raw.Names.Should().OnlyContain(n => n == "_id" || char.IsLower(n[0]), "todos os campos em camelCase: " + string.Join(",", raw.Names));
    }

    [Fact]
    public async Task Idea_WithoutOptionalFields_StoresNullsAndLoadsBack()
    {
        var idea = Builders.Idea();
        await using (var ctx = _db.CreateContext()) { ctx.Ideas.Add(idea); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.Ideas.FirstAsync(x => x.Id == idea.Id);
        loaded.Ice.Should().BeNull();
        loaded.GuidelineId.Should().BeNull();
        loaded.ReviewerId.Should().BeNull();
        loaded.ReviewedAt.Should().BeNull();
    }

    [Fact]
    public async Task Project_Decimals_AreExact_AndStoredAsDecimal128()
    {
        var data = Builders.ProjectData(investment: 1234567.89m, ret: 0.01m) with { CostReduction = 99999.99m };
        var project = Builders.Project(data);
        await using (var ctx = _db.CreateContext()) { ctx.Projects.Add(project); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.Projects.FirstAsync(x => x.Id == project.Id);
        loaded.Investment.Should().Be(1234567.89m);
        loaded.FinancialReturn.Should().Be(0.01m);
        loaded.CostReduction.Should().Be(99999.99m);
        loaded.ProductivityGain.Should().Be(12.5m);
        loaded.Version.Should().Be(1);

        var raw = await _db.Raw(Collections.Projects).Find(new BsonDocument()).SingleAsync();
        raw["investment"].BsonType.Should().Be(BsonType.Decimal128);
        raw["creatorManagerId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["stage"].AsString.Should().Be("PLANEJAMENTO");
    }

    [Fact]
    public async Task Project_Draft_KeepsOriginatingIdeaAsObjectId()
    {
        var idea = Builders.Idea();
        var project = Project.CreateDraftFromIdea(idea, Domain.Common.EntityId.New(), "Gestor", Builders.Now);
        await using (var ctx = _db.CreateContext()) { ctx.Projects.Add(project); await ctx.SaveChangesAsync(); }

        var raw = await _db.Raw(Collections.Projects).Find(new BsonDocument()).SingleAsync();
        raw["originatingIdeaId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["reporterId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["priorityScore"].BsonType.Should().Be(BsonType.Null);
    }

    [Fact]
    public async Task AppUser_RoundTrip_IncludesBadgeListAndIdentityFields()
    {
        var user = Builders.User(Role.GESTOR);
        user.ApplyPoints(30);
        user.AddBadges(["Primeira Ideia", "Estrategista"]);
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.UserName!.ToUpperInvariant();
        user.PasswordHash = "hash";
        user.SecurityStamp = "stamp";
        user.AccessFailedCount = 2;
        user.LockoutEnd = new DateTimeOffset(2026, 9, 21, 13, 0, 0, TimeSpan.Zero);
        await using (var ctx = _db.CreateContext()) { ctx.Users.Add(user); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.Users.FirstAsync(x => x.Id == user.Id);
        loaded.Role.Should().Be(Role.GESTOR);
        loaded.Points.Should().Be(30);
        loaded.Badges.Should().BeEquivalentTo(["Primeira Ideia", "Estrategista"]);
        loaded.AccessFailedCount.Should().Be(2);
        loaded.LockoutEnd.Should().Be(user.LockoutEnd);
        loaded.PasswordHash.Should().Be("hash");

        var raw = await _db.Raw(Collections.Users).Find(new BsonDocument()).SingleAsync();
        raw["role"].AsString.Should().Be("GESTOR");
        raw["badges"].AsBsonArray.Should().HaveCount(2);
        raw["normalizedEmail"].BsonType.Should().Be(BsonType.String);
    }

    [Fact]
    public async Task ProjectUpdate_Changes_AreEmbeddedWithKind()
    {
        var changes = new[]
        {
            FieldChange.Number("investment", 100m, 120m),
            FieldChange.Text("stage", "PLANEJAMENTO", "EM_EXECUCAO"),
            FieldChange.Date("targetDate", null, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc))
        };
        var update = ProjectUpdate.Create(Domain.Common.EntityId.New(), Domain.Common.EntityId.New(), "Gestor", "nota", changes, Builders.Now);
        await using (var ctx = _db.CreateContext()) { ctx.ProjectUpdates.Add(update); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.ProjectUpdates.FirstAsync(x => x.Id == update.Id);
        loaded.Changes.Should().HaveCount(3);
        loaded.Changes[0].Should().BeEquivalentTo(new { Field = "investment", Kind = FieldValueKind.NUMBER, From = "100", To = "120" });
        loaded.Changes[2].From.Should().BeNull();

        var raw = await _db.Raw(Collections.ProjectUpdates).Find(new BsonDocument()).SingleAsync();
        raw["projectId"].BsonType.Should().Be(BsonType.ObjectId);
        raw["changes"][0]["kind"].AsString.Should().Be("NUMBER");
        raw["changes"][0]["field"].AsString.Should().Be("investment");
    }

    [Fact]
    public async Task GuidelineAndHistory_RoundTrip_WithSnapshot()
    {
        var guideline = Builders.Guideline(campaign: "Campanha X", pillar: Pillar.MENSURACAO);
        var entry = GuidelineHistoryEntry.From(guideline, GuidelineAction.CREATED, guideline.AuthorId, "Líder", Builders.Now);
        await using (var ctx = _db.CreateContext())
        {
            ctx.Guidelines.Add(guideline);
            ctx.GuidelineHistory.Add(entry);
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loaded = await read.GuidelineHistory.FirstAsync(x => x.Id == entry.Id);
        loaded.Snapshot.Title.Should().Be(guideline.Title);
        loaded.Snapshot.Pillar.Should().Be(Pillar.MENSURACAO);
        loaded.Category.Should().Be(Pillar.MENSURACAO);
        loaded.Campaign.Should().Be("Campanha X");

        var raw = await _db.Raw(Collections.GuidelineHistory).Find(new BsonDocument()).SingleAsync();
        raw["action"].AsString.Should().Be("CREATED");
        raw["snapshot"]["campaign"].AsString.Should().Be("Campanha X");
        raw["guidelineId"].BsonType.Should().Be(BsonType.ObjectId);
    }

    [Fact]
    public async Task RefreshToken_RoundTrip()
    {
        var token = RefreshToken.Issue(Domain.Common.EntityId.New(), "hash-abc", Builders.Now, TimeSpan.FromDays(7));
        token.Rotate("hash-def", Builders.Now.AddMinutes(1));
        await using (var ctx = _db.CreateContext()) { ctx.RefreshTokens.Add(token); await ctx.SaveChangesAsync(); }

        await using var read = _db.CreateContext();
        var loaded = await read.RefreshTokens.FirstAsync(x => x.TokenHash == "hash-abc");
        loaded.WasRotated.Should().BeTrue();
        loaded.ReplacedByHash.Should().Be("hash-def");
        loaded.ExpiresAt.Should().Be(Builders.Now.AddDays(7));
        loaded.FamilyId.Should().Be(token.FamilyId);
    }
}
