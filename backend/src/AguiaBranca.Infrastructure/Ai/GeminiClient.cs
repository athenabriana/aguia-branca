using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AguiaBranca.Infrastructure.Ai;

/// <summary>
/// Cliente tipado do <c>generateContent</c> do Gemini com saída estruturada (<c>responseSchema</c>). A resiliência
/// (timeout, 1 retry com backoff em 429/5xx, circuit breaker) vem do pipeline do <see cref="HttpClient"/>
/// (<see cref="AiServiceCollectionExtensions.AddGeminiClient"/>). A chave viaja só no header <c>x-goog-api-key</c> e
/// nunca é logada; o log registra apenas modelo, status e latência.
/// </summary>
internal sealed class GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger) : IInsightGenerator
{
    private const double Temperature = 0.3;

    public string Model => options.Value.Model;
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<Result<GeneratedInsight>> GenerateAsync(InsightRequest request, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.IsConfigured)
        {
            logger.LogWarning("IA desabilitada: Gemini:ApiKey e/ou Gemini:Model não configurados.");
            return AiErrors.Unavailable("A IA não está configurada neste ambiente.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint(o))
        {
            Content = JsonContent.Create(BuildBody(request, o))
        };
        message.Headers.Add("x-goog-api-key", o.ApiKey);

        var watch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // o cliente desistiu: não é falha do Gemini
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or OperationCanceledException)
        {
            logger.LogWarning("Gemini {Model} indisponível após {ElapsedMs} ms: {Failure}", o.Model, watch.ElapsedMilliseconds, ex.GetType().Name);
            return AiErrors.Unavailable();
        }

        using (response)
        {
            logger.LogInformation("Gemini {Model} respondeu {Status} em {ElapsedMs} ms", o.Model, (int)response.StatusCode, watch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini {Model} recusou a chamada: HTTP {Status} {GoogleStatus}",
                    o.Model, (int)response.StatusCode, await ReadGoogleStatusAsync(response, ct));
                return AiErrors.Unavailable();
            }

            return await ParseAsync(response, o.Model, ct);
        }
    }

    private static string Endpoint(GeminiOptions o) =>
        $"{o.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(o.Model)}:generateContent";

    private static JsonObject BuildBody(InsightRequest request, GeminiOptions o) => new()
    {
        ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction }) },
        ["contents"] = new JsonArray(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = request.UserContent })
        }),
        ["generationConfig"] = GenerationConfig(o)
    };

    private static JsonObject GenerationConfig(GeminiOptions o)
    {
        var config = new JsonObject
        {
            ["responseMimeType"] = "application/json",
            ["responseSchema"] = GeminiSchemas.Insight(),
            ["temperature"] = Temperature,
            ["maxOutputTokens"] = o.MaxOutputTokens
        };
        if (!string.IsNullOrWhiteSpace(o.ThinkingLevel))
            config["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = o.ThinkingLevel.Trim().ToLowerInvariant() };
        return config;
    }

    private async Task<Result<GeneratedInsight>> ParseAsync(HttpResponseMessage response, string model, CancellationToken ct)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            logger.LogWarning("Gemini {Model}: corpo da resposta não é JSON.", model);
            return AiErrors.InvalidResponse();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Invalid(model, "corpo inesperado");

            if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out var block))
                return Invalid(model, $"prompt bloqueado ({block.GetString()})");

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                return Invalid(model, "sem candidatos");

            LogUsage(root, model);

            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var finish) && finish.GetString() is { } reason && reason != "STOP")
                return Invalid(model, $"finishReason={reason}");

            var text = ExtractText(candidate);
            if (string.IsNullOrWhiteSpace(text)) return Invalid(model, "sem texto");

            return GeminiInsightParser.Parse(text) is { } insight
                ? Result<GeneratedInsight>.Ok(insight)
                : Invalid(model, "fora do schema");
        }
    }

    /// <summary>Tokens consumidos (controle de custo/cota). Só contagens: nada do conteúdo.</summary>
    private void LogUsage(JsonElement root, string model)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage)) return;

        int Count(string name) => usage.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;
        logger.LogInformation("Gemini {Model} tokens: prompt={Prompt} resposta={Output} raciocínio={Thoughts} total={Total}",
            model, Count("promptTokenCount"), Count("candidatesTokenCount"), Count("thoughtsTokenCount"), Count("totalTokenCount"));
    }

    /// <summary>Concatena as partes de texto do candidato, ignorando as de "pensamento" (<c>thought: true</c>).</summary>
    private static string ExtractText(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
            return string.Empty;

        return string.Concat(parts.EnumerateArray()
            .Where(p => !(p.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
            .Select(p => p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null));
    }

    private Result<GeneratedInsight> Invalid(string model, string reason)
    {
        logger.LogWarning("Gemini {Model}: resposta inválida ({Reason}).", model, reason);
        return AiErrors.InvalidResponse();
    }

    /// <summary>Só o campo <c>error.status</c> (ex.: RESOURCE_EXHAUSTED). O texto livre do erro não é logado.</summary>
    private static async Task<string> ReadGoogleStatusAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("status", out var status)
                ? status.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
