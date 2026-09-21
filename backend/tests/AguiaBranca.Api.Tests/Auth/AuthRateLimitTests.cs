using System.Net;
using AguiaBranca.Api.Tests.Support;
using AguiaBranca.Infrastructure.Tests.Support;

namespace AguiaBranca.Api.Tests.Auth;

[Collection(MongoCollection.Name)]
public sealed class AuthRateLimitTests(MongoFixture mongo) : ApiIntegrationTest(mongo)
{
    protected override void Configure(ApiFactory factory) =>
        factory.With("RateLimiting:AuthPermitLimit", "10").With("RateLimiting:AuthWindowSeconds", "60");

    [Fact]
    public async Task EleventhAuthRequestInAMinute_Returns429_WithRetryAfter_InApiFormat()
    {
        for (var i = 1; i <= 10; i++)
            (await LoginAsync("ninguem@aguiabranca.com", "qualquer-senha")).Response.StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, $"requisição {i} ainda dentro do limite");

        var blocked = await LoginAsync("ninguem@aguiabranca.com", "qualquer-senha");

        blocked.Response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Body.GetProperty("code").GetString().Should().Be("RATE_LIMITED");
        blocked.Response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        int.Parse(blocked.Response.Headers.GetValues("Retry-After").Single()).Should().BeInRange(1, 60);
    }

    [Fact]
    public async Task LoginAndRefresh_ShareTheSameBucket()
    {
        for (var i = 0; i < 10; i++) await LoginAsync("ninguem@aguiabranca.com", "qualquer-senha");

        var refresh = await Client.SendAsync(Request(HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = "x" }));

        refresh.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task OtherEndpoints_AreNotAffectedByTheAuthLimit()
    {
        for (var i = 0; i < 11; i++) await LoginAsync("ninguem@aguiabranca.com", "qualquer-senha");

        (await Client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/api/v1/testprobe/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
