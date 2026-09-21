using System.Text.Json;
using System.Text.Json.Serialization;
using AguiaBranca.Application.Common.Abstractions;

namespace AguiaBranca.Infrastructure.Ai;

/// <summary>
/// Valida o JSON devolvido pelo modelo contra o schema esperado e os limites de tamanho. Estrutura errada ou campos
/// obrigatórios ausentes → <c>null</c> (vira <c>AI_INVALID_RESPONSE</c>); textos longos demais são apenas truncados.
/// </summary>
internal static class GeminiInsightParser
{
    public const int MaxItems = 8, SummaryMax = 1500, BulletMax = 400, TitleMax = 120, DetailMax = 600;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict };

    public static GeneratedInsight? Parse(string json)
    {
        Wire? wire;
        try { wire = JsonSerializer.Deserialize<Wire>(json, Options); }
        catch (JsonException) { return null; }

        if (wire is null || string.IsNullOrWhiteSpace(wire.Summary) || wire.Highlights is null || wire.Risks is null || wire.Recommendations is null)
            return null;

        var recommendations = new List<GeneratedRecommendation>();
        foreach (var r in wire.Recommendations.Take(MaxItems))
        {
            if (r is null || string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Detail)
                || !Enum.TryParse<InsightPriority>(r.Priority?.Trim(), ignoreCase: true, out var priority)
                || !Enum.IsDefined(priority))
                return null;

            recommendations.Add(new GeneratedRecommendation(
                Clamp(r.Title, TitleMax), Clamp(r.Detail, DetailMax), priority,
                string.IsNullOrWhiteSpace(r.RelatedGuidelineRef) ? null : r.RelatedGuidelineRef.Trim()));
        }

        return new GeneratedInsight(Clamp(wire.Summary, SummaryMax), Bullets(wire.Highlights), Bullets(wire.Risks), recommendations);
    }

    private static List<string> Bullets(List<string?> items) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i)).Take(MaxItems).Select(i => Clamp(i!, BulletMax)).ToList();

    private static string Clamp(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
    }

    private sealed record Wire(string? Summary, List<string?>? Highlights, List<string?>? Risks, List<WireRecommendation?>? Recommendations);
    private sealed record WireRecommendation(string? Title, string? Detail, string? Priority, string? RelatedGuidelineRef);
}
