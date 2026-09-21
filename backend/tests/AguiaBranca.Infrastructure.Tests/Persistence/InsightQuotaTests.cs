using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Ai;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using AguiaBranca.Infrastructure.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace AguiaBranca.Infrastructure.Tests.Persistence;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class InsightQuotaTests(MongoFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc);
    private TestDatabase _db = null!;
    private MongoInsightQuota _quota = null!;

    public async Task InitializeAsync()
    {
        _db = await fixture.CreateDatabaseAsync();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        _quota = new MongoInsightQuota(_db.Database, clock);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Task<BsonDocument> DocOf(string day) =>
        _db.Raw(Collections.AiUsage).Find(new BsonDocument("_id", day)).SingleAsync();

    [Fact]
    public async Task ConsumesUpToTheLimit_ThenRefuses_WithoutExceedingIt()
    {
        var results = new List<bool>();
        for (var i = 0; i < 5; i++) results.Add(await _quota.TryConsumeAsync("2026-09-21", 3, default));

        results.Should().Equal(true, true, true, false, false);
        (await DocOf("2026-09-21"))["count"].AsInt32.Should().Be(3);
    }

    [Fact]
    public async Task EachDayHasItsOwnCounter_AndTheDocumentExpires()
    {
        (await _quota.TryConsumeAsync("2026-09-21", 1, default)).Should().BeTrue();
        (await _quota.TryConsumeAsync("2026-09-21", 1, default)).Should().BeFalse();
        (await _quota.TryConsumeAsync("2026-09-22", 1, default)).Should().BeTrue("outro dia, outro contador");

        (await DocOf("2026-09-21"))["expiresAt"].ToUniversalTime().Should().Be(Now + IndexInitializer.AiUsageRetention);
    }

    [Fact]
    public async Task ConcurrentReservations_NeverGoPastTheLimit()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => _quota.TryConsumeAsync("2026-09-21", 7, default)));

        results.Count(r => r).Should().Be(7);
        (await DocOf("2026-09-21"))["count"].AsInt32.Should().Be(7);
    }

    [Fact]
    public async Task LoweringTheLimitLater_StopsFurtherReservations()
    {
        for (var i = 0; i < 4; i++) await _quota.TryConsumeAsync("2026-09-21", 10, default);

        (await _quota.TryConsumeAsync("2026-09-21", 4, default)).Should().BeFalse();
        (await DocOf("2026-09-21"))["count"].AsInt32.Should().Be(4);
    }
}
