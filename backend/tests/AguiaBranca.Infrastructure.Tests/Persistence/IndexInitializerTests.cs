using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using AguiaBranca.Infrastructure.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Persistence;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IndexInitializerTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync(withIndexes: false);
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<List<BsonDocument>> IndexesOf(string collection) =>
        await (await _db.Raw(collection).Indexes.ListAsync()).ToListAsync();

    [Fact]
    public async Task EnsureIndexes_IsIdempotent()
    {
        await IndexInitializer.EnsureIndexesAsync(_db.Database);
        var before = (await IndexesOf(Collections.Ideas)).Count;

        var act = () => IndexInitializer.EnsureIndexesAsync(_db.Database);

        await act.Should().NotThrowAsync();
        (await IndexesOf(Collections.Ideas)).Count.Should().Be(before);
    }

    [Theory]
    [InlineData(Collections.Users, "ux_users_normalizedEmail", "ux_users_normalizedUserName", "ix_users_role")]
    [InlineData(Collections.RefreshTokens, "ux_refreshTokens_tokenHash", "ix_refreshTokens_familyId", "ttl_refreshTokens_expiresAt")]
    [InlineData(Collections.Guidelines, "ix_guidelines_updatedAt")]
    [InlineData(Collections.GuidelineHistory, "ix_guidelineHistory_guidelineId_occurredAt", "ix_guidelineHistory_category_occurredAt", "ix_guidelineHistory_campaign_occurredAt")]
    [InlineData(Collections.Ideas, "ix_ideas_authorId_createdAt", "ix_ideas_status_iceScore", "ix_ideas_guidelineId_createdAt")]
    [InlineData(Collections.Projects, "ix_projects_division_updatedAt", "ix_projects_guidelineId_updatedAt", "ix_projects_stage_updatedAt", "ux_projects_originatingIdeaId")]
    [InlineData(Collections.ProjectUpdates, "ix_projectUpdates_projectId_createdAt")]
    [InlineData(Collections.PointEvents, "ix_pointEvents_userId_createdAt", "ix_pointEvents_createdAt")]
    [InlineData(Collections.AiInsights, "ux_aiInsights_cacheKey", "ix_aiInsights_createdAt", "ttl_aiInsights_expiresAt")]
    public async Task EnsureIndexes_CreatesAllExpectedIndexes(string collection, params string[] expected)
    {
        await IndexInitializer.EnsureIndexesAsync(_db.Database);

        (await IndexesOf(collection)).Select(i => i["name"].AsString).Should().Contain(expected);
    }

    [Fact]
    public async Task UniqueIndexes_AreMarkedUnique_AndTtlsHaveExpiry()
    {
        await IndexInitializer.EnsureIndexesAsync(_db.Database);

        var email = (await IndexesOf(Collections.Users)).Single(i => i["name"] == "ux_users_normalizedEmail");
        email["unique"].AsBoolean.Should().BeTrue();
        email.Contains("partialFilterExpression").Should().BeTrue();

        var project = (await IndexesOf(Collections.Projects)).Single(i => i["name"] == "ux_projects_originatingIdeaId");
        project["unique"].AsBoolean.Should().BeTrue();
        project["partialFilterExpression"]["originatingIdeaId"]["$type"].AsString.Should().Be("objectId");

        var refresh = (await IndexesOf(Collections.RefreshTokens)).Single(i => i["name"] == "ttl_refreshTokens_expiresAt");
        refresh["expireAfterSeconds"].ToInt64().Should().Be((long)IndexInitializer.RefreshTokenRetentionAfterExpiry.TotalSeconds);

        var insights = (await IndexesOf(Collections.AiInsights)).Single(i => i["name"] == "ttl_aiInsights_expiresAt");
        insights["expireAfterSeconds"].ToInt64().Should().Be(0);

        var iceScore = (await IndexesOf(Collections.Ideas)).Single(i => i["name"] == "ix_ideas_status_iceScore");
        iceScore["key"].AsBsonDocument.Names.Should().Equal("status", "ice.score");
    }

    [Fact]
    public async Task UniqueUserEmail_IgnoresUsersWithoutEmail_ButRejectsDuplicates()
    {
        await IndexInitializer.EnsureIndexesAsync(_db.Database);
        await using var ctx = _db.CreateContext();

        var a = Builders.User(); a.NormalizedEmail = "A@X.COM"; a.NormalizedUserName = "A@X.COM";
        var b = Builders.User(); b.NormalizedEmail = "B@X.COM"; b.NormalizedUserName = "B@X.COM";
        ctx.Users.AddRange(a, b);
        await ctx.SaveChangesAsync();

        await using var dup = _db.CreateContext();
        var c = Builders.User(); c.NormalizedEmail = "A@X.COM"; c.NormalizedUserName = "C@X.COM";
        dup.Users.Add(c);

        var act = () => _db.CreateUnitOfWork(dup).SaveChangesAsync(default);
        await act.Should().ThrowAsync<AguiaBranca.Application.Common.Exceptions.DuplicateKeyException>()
            .Where(e => e.IndexName == "ux_users_normalizedEmail");
    }

    [Fact]
    public async Task MongoInitializer_StartAsync_VerifiesReplicaSet_AndCreatesIndexes()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AguiaBranca.Infrastructure.Configuration.MongoOptions
        {
            ConnectionString = fixture.ConnectionString, Database = _db.Name
        });
        var initializer = new MongoInitializer(
            fixture.Client, options, Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoInitializer>.Instance);

        await initializer.StartAsync(default);

        (await IndexesOf(Collections.Projects)).Select(i => i["name"].AsString).Should().Contain("ux_projects_originatingIdeaId");
        (await MongoInitializer.IsReplicaSetAsync(fixture.Client, default)).Should().BeTrue();
    }

    [Fact]
    public async Task MongoInitializer_WhenDisabled_DoesNothing()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AguiaBranca.Infrastructure.Configuration.MongoOptions
        {
            ConnectionString = fixture.ConnectionString, Database = _db.Name, InitializeOnStartup = false
        });

        await new MongoInitializer(fixture.Client, options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoInitializer>.Instance).StartAsync(default);

        (await IndexesOf(Collections.Projects)).Should().BeEmpty();
    }
}
