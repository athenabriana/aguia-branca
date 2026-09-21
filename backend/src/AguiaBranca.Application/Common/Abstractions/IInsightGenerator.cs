using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Common.Abstractions;

public enum InsightPriority { ALTA, MEDIA, BAIXA }

/// <summary>Instrução de sistema + conteúdo do usuário (dados delimitados). Nunca contém PII (ver <c>InsightPromptBuilder</c>).</summary>
public sealed record InsightRequest(string SystemInstruction, string UserContent);

/// <param name="RelatedGuidelineRef">Referência curta ("G1"…) da orientação citada, como enviada no prompt; nunca um id real.</param>
public sealed record GeneratedRecommendation(string Title, string Detail, InsightPriority Priority, string? RelatedGuidelineRef);

public sealed record GeneratedInsight(
    string Summary, IReadOnlyList<string> Highlights, IReadOnlyList<string> Risks, IReadOnlyList<GeneratedRecommendation> Recommendations);

/// <summary>
/// Gera os insights com um modelo de IA. Falhas de rede/cota/configuração → <c>AI_UNAVAILABLE</c> (503);
/// resposta fora do schema → <c>AI_INVALID_RESPONSE</c> (502). Nunca devolve conteúdo inventado no lugar do erro.
/// </summary>
public interface IInsightGenerator
{
    /// <summary>Nome do modelo configurado (<c>Gemini:Model</c>); vazio quando a IA não está configurada.</summary>
    string Model { get; }

    /// <summary>Chave e modelo presentes. Sem isso não há chamada externa nem consumo de cota.</summary>
    bool IsConfigured { get; }

    Task<Result<GeneratedInsight>> GenerateAsync(InsightRequest request, CancellationToken ct);
}

/// <summary>Política de cache/cota dos insights (vem de <c>Gemini:*</c>).</summary>
public interface IInsightPolicy
{
    TimeSpan CacheDuration { get; }
    int DailyLimit { get; }
}

/// <summary>Teto diário de gerações (a cota gratuita do modelo é pequena e compartilhada por todos os líderes).</summary>
public interface IInsightQuota
{
    /// <summary>Reserva 1 geração no dia <paramref name="dayKey"/> (yyyy-MM-dd). <c>false</c> = teto atingido. Atômico.</summary>
    Task<bool> TryConsumeAsync(string dayKey, int limit, CancellationToken ct);
}

public static class AiErrors
{
    public const string UnavailableCode = "AI_UNAVAILABLE";
    public const string InvalidResponseCode = "AI_INVALID_RESPONSE";

    public static Error Unavailable(string message = "O serviço de IA está indisponível no momento. Tente novamente em instantes.") =>
        Error.External(UnavailableCode, message);

    public static Error InvalidResponse(string message = "A IA devolveu uma resposta inválida. Tente novamente.") =>
        Error.External(InvalidResponseCode, message);
}
