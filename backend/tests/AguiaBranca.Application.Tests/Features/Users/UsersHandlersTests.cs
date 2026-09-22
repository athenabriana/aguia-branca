using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Users;
using AguiaBranca.Application.Features.Users.List;
using AguiaBranca.Application.Features.Users.Ranking;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Tests.Features.Users;

public class RankingHandlerTests
{
    private readonly InMemoryPointEvents _events = new();
    private readonly InMemoryUsers _users = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc));

    private RankingHandler Handler() => new(_events, _users, _clock, new FakeTimeZone());

    private AppUser Op(string name, int totalPoints = 0, Role role = Role.OPERADOR)
    {
        var u = AppUser.Create(name, $"{Guid.NewGuid():N}@x.com", role, Division.LOGISTICA, DateTime.UtcNow);
        u.ApplyPoints(totalPoints);
        _users.Items.Add(u);
        return u;
    }

    private void Event(AppUser u, int delta, DateTime at) =>
        _events.Items.Add(PointEvent.Create(u.Id, delta, PointReason.IDEA_CREATED, null, at));

    private static DateTime Sep(int day, int hour = 12) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Ranks_ByMonthPoints_Descending_WithIdNameAndPoints()
    {
        var ana = Op("Ana"); var bia = Op("Bia"); var caio = Op("Caio");
        Event(ana, 15, Sep(2)); Event(ana, 50, Sep(3));
        Event(bia, 200, Sep(4));
        Event(caio, 10, Sep(5));

        var result = (await Handler().HandleAsync(new RankingQuery(), default)).Value;

        result.Select(r => (r.Name, r.MonthPoints)).Should().Equal(("Bia", 200), ("Ana", 65), ("Caio", 10));
        result[0].Id.Should().Be(bia.Id);
    }

    [Fact]
    public async Task IgnoresOtherMonths_AndTheMonthBoundaryFollowsTheConfiguredTimeZone()
    {
        var ana = Op("Ana");
        Event(ana, 500, new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc));     // agosto
        Event(ana, 400, new DateTime(2026, 9, 1, 2, 59, 0, DateTimeKind.Utc));      // 31/08 23:59 em São Paulo → agosto
        Event(ana, 7, new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc));         // 01/09 00:00 em São Paulo → setembro
        Event(ana, 900, new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc));      // outubro

        var result = (await Handler().HandleAsync(new RankingQuery(), default)).Value;

        result.Should().ContainSingle().Which.MonthPoints.Should().Be(7);
    }

    [Fact]
    public async Task WhenTheClockCrossesTheMonth_TheRankingResets()
    {
        var ana = Op("Ana");
        Event(ana, 40, Sep(10));

        (await Handler().HandleAsync(new RankingQuery(), default)).Value.Should().ContainSingle();

        _clock.UtcNow = new DateTime(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc); // já é outubro em São Paulo
        (await Handler().HandleAsync(new RankingQuery(), default)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyOperators_AreRanked()
    {
        var gestor = Op("Gestor", role: Role.GESTOR); var lider = Op("Líder", role: Role.LIDER); var op = Op("Operadora");
        Event(gestor, 999, Sep(2)); Event(lider, 999, Sep(2)); Event(op, 5, Sep(2));

        (await Handler().HandleAsync(new RankingQuery(), default)).Value.Select(r => r.Name).Should().Equal("Operadora");
    }

    [Fact]
    public async Task Ties_AreBrokenByTotalPoints_ThenByName()
    {
        var zeca = Op("Zeca", totalPoints: 500); var ana = Op("Ana", totalPoints: 100);
        var beto = Op("Beto", totalPoints: 100); var caio = Op("Caio", totalPoints: 100);
        foreach (var u in new[] { zeca, ana, beto, caio }) Event(u, 30, Sep(3));

        var names = (await Handler().HandleAsync(new RankingQuery(), default)).Value.Select(r => r.Name);

        names.Should().Equal("Zeca", "Ana", "Beto", "Caio");
    }

    [Fact]
    public async Task NetNegativeOrZeroMonths_DoNotAppear()
    {
        var ana = Op("Ana"); var bia = Op("Bia");
        Event(ana, -10, Sep(2));                     // estorno de ideia de mês anterior
        Event(bia, 15, Sep(2)); Event(bia, -15, Sep(3)); // soma zero

        (await Handler().HandleAsync(new RankingQuery(), default)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Limit_TruncatesTheList()
    {
        for (var i = 0; i < 8; i++) Event(Op($"Operador {i}"), 10 + i, Sep(2));

        var result = (await Handler().HandleAsync(new RankingQuery(3), default)).Value;

        result.Should().HaveCount(3);
        result.Select(r => r.MonthPoints).Should().Equal(17, 16, 15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task InvalidLimit_Returns400(int limit)
    {
        var result = await Handler().HandleAsync(new RankingQuery(limit), default);
        result.FirstError.Should().BeEquivalentTo(new { Type = ErrorType.Validation, Field = "limit" });
    }

    [Fact]
    public async Task NoEvents_ReturnsEmpty() =>
        (await Handler().HandleAsync(new RankingQuery(), default)).Value.Should().BeEmpty();
}

public class ListUsersHandlerTests
{
    private readonly InMemoryUsers _users = new();

    [Fact]
    public async Task ListsByRole_SortedByName_WithoutEmail()
    {
        _users.Items.Add(AppUser.Create("Zé", "ze@x.com", Role.GESTOR, Division.LOGISTICA, DateTime.UtcNow));
        _users.Items.Add(AppUser.Create("Ana", "ana@x.com", Role.GESTOR, Division.COMERCIO, DateTime.UtcNow));
        _users.Items.Add(AppUser.Create("Op", "op@x.com", Role.OPERADOR, Division.LOGISTICA, DateTime.UtcNow));

        var gestores = (await new ListUsersHandler(_users).HandleAsync(new ListUsersQuery(Role.GESTOR), default)).Value;
        var all = (await new ListUsersHandler(_users).HandleAsync(new ListUsersQuery(), default)).Value;

        gestores.Select(u => u.Name).Should().Equal("Ana", "Zé");
        all.Should().HaveCount(3);
        typeof(UserSummaryResponse).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(["Id", "Name", "Role", "Division"]);
    }

    [Fact]
    public async Task InvalidRole_Returns400()
    {
        var result = await new ListUsersHandler(_users).HandleAsync(new ListUsersQuery((Role)42), default);
        result.FirstError.Field.Should().Be("role");
    }
}
