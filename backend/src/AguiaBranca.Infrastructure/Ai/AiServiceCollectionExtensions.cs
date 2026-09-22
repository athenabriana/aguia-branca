using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace AguiaBranca.Infrastructure.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="IInsightGenerator"/> (Gemini) com o pipeline padrão de resiliência ajustado: timeout por
    /// tentativa = <c>Gemini:TimeoutSeconds</c> (20 s), 1 nova tentativa com backoff em 429/5xx/timeout e circuit breaker
    /// (abre com ≥ 50 % de falhas em ≥ 4 chamadas na janela de 60 s; fica 30 s aberto).
    /// </summary>
    public static IHttpClientBuilder AddGeminiClient(this IServiceCollection services)
    {
        var builder = services.AddHttpClient<IInsightGenerator, GeminiClient>();

        // Com log em Trace o HttpClient registra os headers da requisição — inclusive x-goog-api-key. Redige todos.
        builder.RedactLoggedHeaders(_ => true);

        builder.AddStandardResilienceHandler().Configure((resilience, sp) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
            var attempt = TimeSpan.FromSeconds(o.TimeoutSeconds);
            var delay = TimeSpan.FromMilliseconds(o.RetryDelayMilliseconds);

            // TimeoutSeconds vale por tentativa: com o retry o pior caso é ≈ 2× + backoff (folga de 2 s para o jitter).
            resilience.AttemptTimeout.Timeout = attempt;
            resilience.TotalRequestTimeout.Timeout = attempt * 2 + delay * 2 + TimeSpan.FromSeconds(2);

            resilience.Retry.MaxRetryAttempts = 1;
            resilience.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            resilience.Retry.UseJitter = true;
            resilience.Retry.Delay = delay;

            resilience.CircuitBreaker.FailureRatio = 0.5;
            resilience.CircuitBreaker.MinimumThroughput = 4;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(60, o.TimeoutSeconds * 3)); // ≥ 2× o timeout da tentativa
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        });

        return builder;
    }
}
