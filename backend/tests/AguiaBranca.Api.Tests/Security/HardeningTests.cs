using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using AguiaBranca.Api.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AguiaBranca.Api.Tests.Security;

/// <summary>B21 — CORS, limite de payload, headers de segurança, HSTS, ids inválidos e vazamento de detalhes.</summary>
public sealed class HardeningTests
{
    private const string Allowed = "https://painel.exemplo.com";

    private static HttpClient Client(ApiFactory factory, bool authenticated = false, string? baseAddress = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress ?? "http://localhost") });
        if (authenticated)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Forge("665f00000000000000000001"));
        return client;
    }

    private static string? Header(HttpResponseMessage r, string name) =>
        r.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    // ── headers ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/")]                                  // 200
    [InlineData("/api/v1/guidelines")]                 // 401 anônimo
    [InlineData("/api/v1/testprobe/boom")]             // 500
    [InlineData("/rota-que-nao-existe")]               // 401/404
    public async Task EveryResponse_CarriesTheSecurityHeaders_EvenErrors(string path)
    {
        using var factory = new ApiFactory();
        var response = await Client(factory).GetAsync(path);

        Header(response, "X-Content-Type-Options").Should().Be("nosniff");
        Header(response, "Referrer-Policy").Should().Be("no-referrer");
        Header(response, "X-Frame-Options").Should().Be("DENY");
        Header(response, "Content-Security-Policy").Should().Contain("default-src 'none'").And.Contain("frame-ancestors 'none'");
        response.Headers.Contains("Server").Should().BeFalse();
    }

    [Fact]
    public async Task AuthResponses_AreNeverCached()
    {
        using var factory = new ApiFactory();
        var response = await Client(factory).GetAsync("/api/v1/auth/me");

        Header(response, "Cache-Control").Should().Be("no-store");
        Header(response, "Pragma").Should().Be("no-cache");
    }

    [Fact]
    public async Task NonAuthResponses_DoNotForceNoStore()
    {
        using var factory = new ApiFactory();
        (await Client(factory).GetAsync("/")).Headers.CacheControl.Should().BeNull();
    }

    // ── CORS ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cors_AllowsOnlyTheConfiguredOrigins_WithPreflight()
    {
        using var factory = new ApiFactory().With("Cors:Origins:0", Allowed);
        using var client = Client(factory);

        var simple = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/") { Headers = { { "Origin", Allowed } } });
        Header(simple, "Access-Control-Allow-Origin").Should().Be(Allowed);
        Header(simple, "Access-Control-Allow-Credentials").Should().BeNull();

        var preflight = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/guidelines")
        {
            Headers = { { "Origin", Allowed }, { "Access-Control-Request-Method", "PUT" }, { "Access-Control-Request-Headers", "authorization,content-type" } }
        });
        preflight.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Header(preflight, "Access-Control-Allow-Origin").Should().Be(Allowed);
        Header(preflight, "Access-Control-Allow-Methods").Should().Contain("PUT");
        Header(preflight, "Access-Control-Allow-Headers")!.ToLowerInvariant().Should().Contain("authorization").And.Contain("content-type");
    }

    [Theory]
    [InlineData("https://malicioso.example")]
    [InlineData("http://painel.exemplo.com")]          // esquema diferente
    [InlineData("https://painel.exemplo.com.evil.io")] // sufixo
    public async Task Cors_UnlistedOrigin_GetsNoAllowOriginHeader(string origin)
    {
        using var factory = new ApiFactory().With("Cors:Origins:0", Allowed);
        using var client = Client(factory);

        var simple = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/") { Headers = { { "Origin", origin } } });
        var preflight = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/guidelines")
        {
            Headers = { { "Origin", origin }, { "Access-Control-Request-Method", "GET" } }
        });

        Header(simple, "Access-Control-Allow-Origin").Should().BeNull();
        Header(preflight, "Access-Control-Allow-Origin").Should().BeNull();
    }

    [Fact]
    public async Task Cors_WithNoConfiguredOrigins_AllowsNoBrowserOrigin()
    {
        using var factory = new ApiFactory();
        var response = await Client(factory).SendAsync(new HttpRequestMessage(HttpMethod.Get, "/") { Headers = { { "Origin", Allowed } } });

        Header(response, "Access-Control-Allow-Origin").Should().BeNull();
    }

    // ── payload ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayloadAboveTheLimit_Returns413ProblemJson()
    {
        using var factory = new ApiFactory();
        var body = new StringContent("{\"name\":\"" + new string('a', 2 * 1024 * 1024) + "\"}", Encoding.UTF8, "application/json");

        var response = await Client(factory, authenticated: true).PostAsync("/api/v1/testprobe/echo", body);
        var text = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        text.Should().Contain("PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public async Task PayloadJustUnderTheLimit_IsAccepted_AndTheLimitIsConfigurable()
    {
        using var factory = new ApiFactory();
        var ok = await Client(factory, authenticated: true).PostAsync("/api/v1/testprobe/echo",
            new StringContent("{\"name\":\"" + new string('a', 900 * 1024) + "\"}", Encoding.UTF8, "application/json"));
        ok.StatusCode.Should().NotBe(HttpStatusCode.RequestEntityTooLarge, "900 KB < 1 MB (falha de validação 400 é esperada: o nome tem no máx. 5)");

        using var small = new ApiFactory().With("Security:MaxRequestBodyBytes", "2048");
        var blocked = await Client(small, authenticated: true).PostAsync("/api/v1/testprobe/echo",
            new StringContent("{\"name\":\"" + new string('a', 4096) + "\"}", Encoding.UTF8, "application/json"));
        blocked.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    // ── HSTS / proxy ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hsts_IsSentOverHttps_NotOverPlainHttp()
    {
        using var factory = new ApiFactory(); // ambiente "Testing" (≠ Development)

        var https = await Client(factory, baseAddress: "https://api.exemplo.com").GetAsync("/");
        var http = await Client(factory, baseAddress: "http://api.exemplo.com").GetAsync("/");

        Header(https, "Strict-Transport-Security").Should().Be("max-age=31536000; includeSubDomains");
        Header(http, "Strict-Transport-Security").Should().BeNull();
    }

    [Fact]
    public async Task Hsts_IsNotSentInDevelopment()
    {
        using var factory = new ApiFactory { Environment = "Development" };
        (await Client(factory, baseAddress: "https://api.exemplo.com").GetAsync("/")).Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task ForwardedProto_IsHonoredOnlyWhenTheProxyIsTrusted()
    {
        var request = () => new HttpRequestMessage(HttpMethod.Get, "/") { Headers = { { "X-Forwarded-Proto", "https" } } };

        using var trusted = new ApiFactory().With("Security:ForwardedHeaders", "true");
        using var untrusted = new ApiFactory();

        Header(await Client(trusted, baseAddress: "http://api.exemplo.com").SendAsync(request()), "Strict-Transport-Security").Should().NotBeNull();
        Header(await Client(untrusted, baseAddress: "http://api.exemplo.com").SendAsync(request()), "Strict-Transport-Security").Should().BeNull();
    }

    // ── ids inválidos e vazamento ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/ideas/xyz")]
    [InlineData("/api/v1/ideas/xyz/ice")]
    [InlineData("/api/v1/projects/12345")]
    [InlineData("/api/v1/projects/xyz/updates")]
    [InlineData("/api/v1/guidelines/%3Cscript%3E")]
    [InlineData("/api/v1/reports/projects/xyz")]
    [InlineData("/api/v1/reports/guidelines/'%20OR%201=1")]
    [InlineData("/api/v1/testprobe/items/not-an-id")]
    public async Task MalformedRouteIds_AreRejectedAs4xx_NeverA500(string path)
    {
        using var factory = new ApiFactory();
        var response = await Client(factory, authenticated: true).GetAsync(path);

        // 404 (rota não casa) — ou 405 quando só o verbo difere (ex.: GET num endpoint PUT); nunca 5xx.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("GET", "/api/v1/testprobe/boom")]
    [InlineData("GET", "/api/v1/testprobe/validation")]
    [InlineData("GET", "/api/v1/testprobe/duplicate")]
    [InlineData("GET", "/api/v1/testprobe/payload-too-large")]
    [InlineData("GET", "/api/v1/nao-existe")]
    [InlineData("DELETE", "/api/v1/testprobe/boom")]
    [InlineData("POST", "/api/v1/testprobe/echo")]      // sem corpo
    public async Task ErrorResponses_NeverContainStackTracesOrInternalNames(string method, string path)
    {
        using var factory = new ApiFactory();
        var client = Client(factory, authenticated: true);
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        var text = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        Regex.IsMatch(text, @"\bat [A-Za-z_.]+\(").Should().BeFalse("nenhum frame de stack trace");
        text.Should().NotContainAny("Exception", "StackTrace", "AguiaBranca.", "System.", "Microsoft.", ".cs:line", "segredo-interno-123");
    }

    // ── segredos na configuração ──────────────────────────────────────────────────────────────

    [Fact]
    public void ShippedAppsettings_ContainNoSecrets()
    {
        var files = Directory.GetFiles(AppContext.BaseDirectory, "appsettings*.json");
        files.Should().NotBeEmpty();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("mongodb+srv://", $"{Path.GetFileName(file)}: string de conexão com credenciais");
            Regex.IsMatch(text, "\"(Key|ApiKey)\"\\s*:\\s*\"[^\"]+\"").Should().BeFalse($"{Path.GetFileName(file)}: segredo preenchido");
            Regex.IsMatch(text, "AIza[0-9A-Za-z_-]{20,}").Should().BeFalse($"{Path.GetFileName(file)}: chave do Google");
        }
    }
}
