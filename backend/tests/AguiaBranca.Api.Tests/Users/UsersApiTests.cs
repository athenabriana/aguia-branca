using System.Net;
using System.Text.Json;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AguiaBranca.Api.Tests.Users;

[Trait("Category", "Integration")]
public sealed class UsersApiTests(SeededApiFixture api) : IClassFixture<SeededApiFixture>
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    // ---------- GET /users ----------

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Role.OPERADOR, HttpStatusCode.Forbidden)]
    [InlineData(Role.GESTOR, HttpStatusCode.OK)]
    [InlineData(Role.LIDER, HttpStatusCode.OK)]
    public async Task ListUsers_OnlyGestorAndLider(Role? role, HttpStatusCode expected)
    {
        using var client = api.ClientAs(role);
        (await client.GetAsync("/api/v1/users")).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListUsers_ReturnsIdNameRoleDivision_SortedByName_WithoutSensitiveFields()
    {
        using var gestor = api.ClientAs(Role.GESTOR);

        var response = await gestor.GetAsync("/api/v1/users");
        var raw = await response.Content.ReadAsStringAsync();
        var users = JsonDocument.Parse(raw).RootElement.EnumerateArray().ToList();

        users.Should().HaveCountGreaterThanOrEqualTo(5);
        users.Select(u => u.GetProperty("name").GetString()).Should().BeInAscendingOrder(StringComparer.CurrentCultureIgnoreCase);
        users.Should().OnlyContain(u => u.EnumerateObject().Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] { "division", "id", "name", "role" }));
        raw.Should().NotContainAny("email", "aguiabranca.com", "passwordHash", "points", "badges");
    }

    [Fact]
    public async Task ListUsers_FiltersByRole()
    {
        using var lider = api.ClientAs(Role.LIDER);

        var gestores = (await Json(await lider.GetAsync("/api/v1/users?role=GESTOR"))).EnumerateArray().ToList();
        var operadores = (await Json(await lider.GetAsync("/api/v1/users?role=OPERADOR"))).EnumerateArray().ToList();

        gestores.Should().OnlyContain(u => u.GetProperty("role").GetString() == "GESTOR").And.NotBeEmpty();
        operadores.Should().OnlyContain(u => u.GetProperty("role").GetString() == "OPERADOR");
        operadores.Select(u => u.GetProperty("name").GetString()).Should().Contain(["Operador INOVAGAB", "Ana Operadora", "Bruno Operador"]);
    }

    [Fact]
    public async Task ListUsers_InvalidRole_Returns400()
    {
        using var gestor = api.ClientAs(Role.GESTOR);
        (await gestor.GetAsync("/api/v1/users?role=CHEFE")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- GET /users/ranking ----------

    private static DateTime StartOfCurrentMonthUtc()
    {
        var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, sp);
        return TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), sp);
    }

    [Theory]
    [InlineData(Role.OPERADOR)]
    [InlineData(Role.GESTOR)]
    [InlineData(Role.LIDER)]
    public async Task Ranking_IsOpenToEveryAuthenticatedRole(Role role)
    {
        using var client = api.ClientAs(role);
        (await client.GetAsync("/api/v1/users/ranking")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ranking_RequiresAuthentication()
    {
        using var anon = api.ClientAs((Role?)null);
        (await anon.GetAsync("/api/v1/users/ranking")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ranking_MatchesTheCurrentMonthEvents_OnlyOperators_SortedDescending()
    {
        using var operador = api.ClientAs(Role.OPERADOR);
        var start = StartOfCurrentMonthUtc();

        var ranking = (await Json(await operador.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray().ToList();

        await using var ctx = api.Db.CreateContext();
        var users = await ctx.Users.AsNoTracking().ToListAsync();
        var events = await ctx.PointEvents.AsNoTracking().Where(e => e.CreatedAt >= start).ToListAsync();
        var expected = events
            .GroupBy(e => e.UserId)
            .Select(g => (User: users.Single(u => u.Id == g.Key), Points: g.Sum(e => e.Delta)))
            .Where(x => x.User.Role == Role.OPERADOR && x.Points > 0)
            .ToDictionary(x => x.User.Id, x => x.Points);

        ranking.ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("monthPoints").GetInt32())
            .Should().BeEquivalentTo(expected);
        ranking.Select(r => r.GetProperty("monthPoints").GetInt32()).Should().BeInDescendingOrder();
        ranking.Should().OnlyContain(r => r.EnumerateObject().Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] { "id", "monthPoints", "name" }));
        ranking.Select(r => r.GetProperty("name").GetString()).Should().NotContain(["Gestor INOVAGAB", "Líder INOVAGAB"]);
    }

    [Fact]
    public async Task Ranking_IgnoresPointsFromPreviousMonths()
    {
        using var operador = api.ClientAs(Role.OPERADOR);
        var before = (await Json(await operador.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("monthPoints").GetInt32());

        // Um evento enorme, mas do mês anterior, para uma operadora: não pode alterar o ranking do mês.
        await using (var ctx = api.Db.CreateContext())
        {
            var ana = await ctx.Users.AsNoTracking().FirstAsync(u => u.Name == "Ana Operadora");
            ctx.PointEvents.Add(PointEvent.Create(ana.Id, 100_000, PointReason.IDEA_IMPLEMENTED, null, StartOfCurrentMonthUtc().AddDays(-2)));
            await ctx.SaveChangesAsync();
        }

        var after = (await Json(await operador.GetAsync("/api/v1/users/ranking?limit=50"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("monthPoints").GetInt32());
        after.Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Ranking_LimitParameter_TruncatesTheList()
    {
        using var operador = api.ClientAs(Role.OPERADOR);
        (await Json(await operador.GetAsync("/api/v1/users/ranking?limit=1"))).GetArrayLength().Should().BeLessThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("limit=abc")]
    public async Task Ranking_InvalidLimit_Returns400(string query)
    {
        using var operador = api.ClientAs(Role.OPERADOR);
        (await operador.GetAsync($"/api/v1/users/ranking?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
