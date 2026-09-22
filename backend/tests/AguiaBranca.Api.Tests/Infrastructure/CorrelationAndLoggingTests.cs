using AguiaBranca.Api.Tests.Support;
using Serilog.Events;

namespace AguiaBranca.Api.Tests.Infrastructure;

public sealed class CorrelationAndLoggingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CorrelationAndLoggingTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidIncomingCorrelationId_IsEchoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Correlation-ID", "abc-12345_678");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-ID").Single().Should().Be("abc-12345_678");
    }

    [Fact]
    public async Task MissingCorrelationId_IsGenerated()
    {
        var response = await _client.GetAsync("/");
        response.Headers.GetValues("X-Correlation-ID").Single().Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Theory]
    [InlineData("curto")]
    [InlineData("tem espaco e caracteres <perigosos>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task UnsafeCorrelationId_IsReplaced(string incoming)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", incoming);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-ID").Single().Should().NotBe(incoming).And.MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task ErrorResponses_AlsoCarryTheCorrelationId()
    {
        var response = await _client.GetAsync("/api/v1/nao-existe");
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();
    }

    [Fact]
    public async Task LogEvents_CarryCorrelationId_AndNeverTheSecretMessageToTheClient()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/testprobe/boom");
        request.Headers.Add("X-Correlation-ID", "log-check-000111");
        request.Headers.Add("Authorization", "Bearer super-secret-token");

        await _client.SendAsync(request);

        var errors = _factory.Logs.Events.Where(e => e.Level == LogEventLevel.Error &&
            e.Properties.TryGetValue("CorrelationId", out var v) && v.ToString().Contains("log-check-000111")).ToList();
        errors.Should().NotBeEmpty("o erro inesperado deve ser logado com o CorrelationId");

        _factory.Logs.Events
            .Select(e => e.RenderMessage() + string.Join(",", e.Properties.Values.Select(p => p.ToString())))
            .Should().NotContain(m => m.Contains("super-secret-token"), "o header Authorization nunca pode aparecer em log");
    }

    [Fact]
    public async Task ClientErrors_AreNotLoggedAsErrors()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/testprobe/domain/IDEA_NOT_EDITABLE");
        request.Headers.Add("X-Correlation-ID", "client-error-0001");

        await _client.SendAsync(request);

        _factory.Logs.Events
            .Where(e => e.Properties.TryGetValue("CorrelationId", out var v) && v.ToString().Contains("client-error-0001"))
            .Should().NotContain(e => e.Level == LogEventLevel.Error);
    }
}
