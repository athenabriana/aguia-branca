using System.Net;
using AguiaBranca.Application.Common.Abstractions;
using static AguiaBranca.Infrastructure.Tests.Ai.GeminiTestKit;

namespace AguiaBranca.Infrastructure.Tests.Ai;

public class GeminiClientTests
{
    [Fact]
    public async Task Success_ReturnsTheStructuredInsight_AndSendsTheContractedRequest()
    {
        var handler = new FakeGeminiHandler().Then(HttpStatusCode.OK, Ok());
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(new InsightRequest("SISTEMA-X", "USUARIO-Y"), default);

        result.IsSuccess.Should().BeTrue();
        var insight = result.Value;
        insight.Summary.Should().Be("Resumo executivo.");
        insight.Highlights.Should().Equal("Destaque 1");
        insight.Risks.Should().Equal("Risco 1");
        insight.Recommendations.Should().ContainSingle().Which.Should()
            .Be(new GeneratedRecommendation("Ação", "Detalhe", InsightPriority.ALTA, "G1"));

        var sent = handler.Requests.Single();
        sent.Uri.AbsoluteUri.Should().Be($"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
        sent.Headers["x-goog-api-key"].Should().Be(ApiKey);
        sent.Uri.Query.Should().BeEmpty("a chave nunca vai na query string");
        var body = sent.Json;
        body.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString().Should().Be("SISTEMA-X");
        body.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString().Should().Be("USUARIO-Y");
        var config = body.GetProperty("generationConfig");
        config.GetProperty("responseMimeType").GetString().Should().Be("application/json");
        config.GetProperty("temperature").GetDouble().Should().Be(0.3);
        config.GetProperty("maxOutputTokens").GetInt32().Should().Be(2048);
        config.GetProperty("responseSchema").GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("summary", "highlights", "risks", "recommendations");
        config.GetProperty("responseSchema").GetProperty("properties").GetProperty("recommendations").GetProperty("items")
            .GetProperty("properties").GetProperty("priority").GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("ALTA", "MEDIA", "BAIXA");
    }

    [Fact]
    public async Task TooManyRequestsThenOk_RetriesOnceAndSucceeds()
    {
        var handler = new FakeGeminiHandler()
            .Then(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"status":"RESOURCE_EXHAUSTED"}}""")
            .Then(HttpStatusCode.OK, Ok());
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.IsSuccess.Should().BeTrue();
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task PersistentServerError_FailsAfterOneRetry_AsAiUnavailable()
    {
        var handler = new FakeGeminiHandler().Always(HttpStatusCode.InternalServerError, "{}");
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        handler.Calls.Should().Be(2, "1 tentativa + 1 retry");
    }

    [Fact]
    public async Task Timeout_IsAiUnavailable_AndDoesNotHang()
    {
        var handler = new FakeGeminiHandler().Hang();
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var started = DateTime.UtcNow;
        var result = await client.GenerateAsync(Request, default);

        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("[]")]
    [InlineData("""{"summary":"","highlights":[],"risks":[],"recommendations":[]}""")]
    [InlineData("""{"summary":"ok","highlights":[],"risks":[]}""")]
    [InlineData("""{"summary":"ok","highlights":[],"risks":[],"recommendations":[{"title":"t","detail":"d","priority":"URGENTE"}]}""")]
    [InlineData("""{"summary":"ok","highlights":[],"risks":[],"recommendations":[{"title":"","detail":"d","priority":"ALTA"}]}""")]
    [InlineData("""{"summary":"ok","highlights":"não é lista","risks":[],"recommendations":[]}""")]
    public async Task OutOfSchemaContent_IsAiInvalidResponse_WithoutRetrying(string modelText)
    {
        var handler = new FakeGeminiHandler().Always(HttpStatusCode.OK, Envelope(modelText));
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.FirstError.Code.Should().Be("AI_INVALID_RESPONSE");
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"}}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"{}"}]},"finishReason":"MAX_TOKENS"}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"STOP"}]}""")]
    [InlineData("não é json")]
    public async Task UnusableEnvelope_IsAiInvalidResponse(string body)
    {
        var handler = new FakeGeminiHandler().Always(HttpStatusCode.OK, body);
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        (await client.GenerateAsync(Request, default)).FirstError.Code.Should().Be("AI_INVALID_RESPONSE");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ClientErrors_AreAiUnavailable_AndNotRetried(HttpStatusCode status)
    {
        var handler = new FakeGeminiHandler().Always(status, """{"error":{"status":"PERMISSION_DENIED","message":"texto livre"}}""");
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ThoughtPartsAreIgnored_AndTextPartsAreConcatenated()
    {
        const string body = """
            {"candidates":[{"finishReason":"STOP","content":{"parts":[
              {"thought":true,"text":"raciocínio interno que não é JSON"},
              {"text":"{\"summary\":\"Resumo\",\"highlights\":[],"},
              {"text":"\"risks\":[],\"recommendations\":[]}"}]}}]}
            """;
        var handler = new FakeGeminiHandler().Then(HttpStatusCode.OK, body);
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("Resumo");
    }

    [Fact]
    public async Task WithoutApiKeyOrModel_IsAiUnavailable_WithoutCallingGemini()
    {
        foreach (var options in new[] { Options(o => o.ApiKey = ""), Options(o => o.Model = "") })
        {
            var handler = new FakeGeminiHandler();
            var (client, _, provider) = Build(handler, options);
            using var _1 = provider;

            (await client.GenerateAsync(Request, default)).FirstError.Code.Should().Be("AI_UNAVAILABLE");
            handler.Calls.Should().Be(0);
        }
    }

    [Fact]
    public async Task TheApiKeyNeverAppearsInLogs_OnSuccessFailureAndInvalidResponses()
    {
        var handler = new FakeGeminiHandler()
            .Then(HttpStatusCode.OK, Ok())
            .Then(HttpStatusCode.OK, Envelope("lixo"))
            .Then(HttpStatusCode.Forbidden, "{\"error\":{\"status\":\"PERMISSION_DENIED\",\"message\":\"key " + ApiKey + " inválida\"}}")
            .Then(_ => throw new HttpRequestException("falha de rede"))
            .Then(_ => throw new HttpRequestException("falha de rede")); // 2ª tentativa do retry
        var (client, logs, provider) = Build(handler);
        using var _1 = provider;

        for (var i = 0; i < 4; i++) await client.GenerateAsync(Request, default);

        // O provedor de log coleta desde Trace: cobre também o log de headers do HttpClient (regressão real: vazava a chave).
        logs.Lines.Should().NotBeEmpty();
        logs.Lines.Should().NotContain(l => l.Contains(ApiKey, StringComparison.Ordinal));
        logs.Lines.Should().Contain(l => l.Contains(Model) && l.Contains("ms"), "o log registra modelo e latência");
    }

    [Fact]
    public async Task CallerCancellation_Propagates_AndIsNotReportedAsUnavailable()
    {
        var handler = new FakeGeminiHandler().Hang();
        var (client, _, provider) = Build(handler, Options(o => o.TimeoutSeconds = 20));
        using var _1 = provider;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => client.GenerateAsync(Request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterRepeatedFailures_AndStopsCallingGemini()
    {
        var handler = new FakeGeminiHandler().Always(HttpStatusCode.InternalServerError, "{}");
        var (client, _, provider) = Build(handler);
        using var _1 = provider;

        for (var i = 0; i < 3; i++) await client.GenerateAsync(Request, default); // 6 tentativas com falha ≥ mínimo de 4
        var callsBefore = handler.Calls;

        var result = await client.GenerateAsync(Request, default);

        result.FirstError.Code.Should().Be("AI_UNAVAILABLE");
        handler.Calls.Should().Be(callsBefore, "com o circuito aberto a chamada nem sai do processo");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("minimal", true)]
    [InlineData("LOW", true)]
    public async Task ThinkingLevel_IsSentOnlyWhenConfigured(string level, bool sent)
    {
        var handler = new FakeGeminiHandler().Then(HttpStatusCode.OK, Ok());
        var (client, _, provider) = Build(handler, Options(o => o.ThinkingLevel = level));
        using var _1 = provider;

        await client.GenerateAsync(Request, default);

        var config = handler.Requests.Single().Json.GetProperty("generationConfig");
        config.TryGetProperty("thinkingConfig", out var thinking).Should().Be(sent);
        if (sent) thinking.GetProperty("thinkingLevel").GetString().Should().Be(level.ToLowerInvariant());
    }

    [Fact]
    public async Task TokenUsage_IsLoggedAsCountsOnly()
    {
        var body = """
            {"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"summary\":\"Resumo\",\"highlights\":[],\"risks\":[],\"recommendations\":[]}"}]}}],
             "usageMetadata":{"promptTokenCount":812,"candidatesTokenCount":233,"thoughtsTokenCount":40,"totalTokenCount":1085}}
            """;
        var handler = new FakeGeminiHandler().Then(HttpStatusCode.OK, body);
        var (client, logs, provider) = Build(handler);
        using var _1 = provider;

        await client.GenerateAsync(Request, default);

        logs.Lines.Should().Contain(l => l.Contains("prompt=812") && l.Contains("resposta=233") && l.Contains("raciocínio=40") && l.Contains("total=1085"));
        logs.Lines.Should().NotContain(l => l.Contains("Resumo"), "o conteúdo gerado não é logado");
    }

    [Fact]
    public async Task TimeoutIsPerAttempt_SoARetryStillFitsInsideTheTotalBudget()
    {
        var handler = new FakeGeminiHandler()
            .Then(async ct => { await Task.Delay(Timeout.Infinite, ct); return FakeGeminiHandler.Response(HttpStatusCode.OK, "{}"); }) // 1ª tentativa trava
            .Then(HttpStatusCode.OK, Ok());
        var (client, _, provider) = Build(handler, Options(o => o.TimeoutSeconds = 1));
        using var _1 = provider;

        var result = await client.GenerateAsync(Request, default);

        result.IsSuccess.Should().BeTrue("a 2ª tentativa recebe o próprio timeout completo");
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task ModelAndBaseUrlComeFromConfiguration()
    {
        var handler = new FakeGeminiHandler().Then(HttpStatusCode.OK, Ok());
        var (client, _, provider) = Build(handler, Options(o => { o.Model = "outro-modelo"; o.BaseUrl = "https://exemplo.test/v9/"; }));
        using var _1 = provider;

        await client.GenerateAsync(Request, default);

        handler.Requests.Single().Uri.AbsoluteUri.Should().Be("https://exemplo.test/v9/models/outro-modelo:generateContent");
        client.Model.Should().Be("outro-modelo");
    }
}
