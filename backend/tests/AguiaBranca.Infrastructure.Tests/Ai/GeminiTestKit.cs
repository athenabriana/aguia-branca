using System.Net;
using System.Text;
using System.Text.Json;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Ai;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Tests.Ai;

/// <summary>Handler que responde por script e registra as requisições (URL, headers e corpo).</summary>
internal sealed class FakeGeminiHandler : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _script = new();
    private Func<CancellationToken, Task<HttpResponseMessage>>? _repeat;

    public List<RecordedRequest> Requests { get; } = [];
    public int Calls => Requests.Count;

    public FakeGeminiHandler Then(HttpStatusCode status, string body) => Then(_ => Task.FromResult(Response(status, body)));
    public FakeGeminiHandler Then(Func<CancellationToken, Task<HttpResponseMessage>> step) { _script.Enqueue(step); return this; }
    public FakeGeminiHandler Always(HttpStatusCode status, string body) { _repeat = _ => Task.FromResult(Response(status, body)); return this; }
    public FakeGeminiHandler Hang() { _repeat = async ct => { await Task.Delay(Timeout.Infinite, ct); return Response(HttpStatusCode.OK, "{}"); }; return this; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.RequestUri!, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

        var step = _script.Count > 0 ? _script.Dequeue() : _repeat ?? throw new InvalidOperationException("Sem resposta programada.");
        return await step(cancellationToken);
    }

    public static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

internal sealed record RecordedRequest(Uri Uri, Dictionary<string, string> Headers, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}

/// <summary>Coleta tudo o que é logado (mensagem formatada + exceção) para procurar vazamento de segredo.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<string> Lines { get; } = [];
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Lines);
    public void Dispose() { }

    private sealed class CapturingLogger(List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (lines) lines.Add(formatter(state, exception) + exception);
        }
    }
}

internal static class GeminiTestKit
{
    public const string ApiKey = "AIzaSy-SEGREDO-que-nao-pode-vazar-0123";
    public const string Model = "modelo-configurado-x";

    public const string ValidInsight =
        """{"summary":"Resumo executivo.","highlights":["Destaque 1"],"risks":["Risco 1"],"recommendations":[{"title":"Ação","detail":"Detalhe","priority":"ALTA","relatedGuidelineRef":"G1"}]}""";

    public static string Envelope(string text, string finishReason = "STOP") =>
        JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason } } });

    public static string Ok(string insightJson = ValidInsight) => Envelope(insightJson);

    public static GeminiOptions Options(Action<GeminiOptions>? tweak = null)
    {
        var o = new GeminiOptions { ApiKey = ApiKey, Model = Model, TimeoutSeconds = 1, RetryDelayMilliseconds = 10 };
        tweak?.Invoke(o);
        return o;
    }

    public static readonly InsightRequest Request = new("system", "<dados>{}</dados>");

    /// <summary>Monta o cliente pelo mesmo caminho de DI de produção (pipeline de resiliência incluído).</summary>
    public static (IInsightGenerator Client, CapturingLoggerProvider Logs, ServiceProvider Provider) Build(
        FakeGeminiHandler handler, GeminiOptions? options = null)
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options ?? Options()));
        services.AddGeminiClient().ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IInsightGenerator>(), logs, provider);
    }
}
