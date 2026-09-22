using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Reports.Insights;

public sealed record GenerateInsightsCommand(Period Period = Period.ALL, Division? Division = null, string? GuidelineId = null, bool Refresh = false);

public sealed record InsightRecommendation(string Title, string Detail, InsightPriority Priority, string? RelatedGuidelineId);

/// <summary>Conteúdo gerado (é o que vai para o cache).</summary>
public sealed record InsightContent(
    string Summary, IReadOnlyList<string> Highlights, IReadOnlyList<string> Risks, IReadOnlyList<InsightRecommendation> Recommendations);

public sealed record InsightResponse(
    string Summary, IReadOnlyList<string> Highlights, IReadOnlyList<string> Risks, IReadOnlyList<InsightRecommendation> Recommendations,
    DateTime GeneratedAt, string Model, bool FromCache)
{
    public static InsightResponse From(InsightContent c, DateTime generatedAt, string model, bool fromCache) =>
        new(c.Summary, c.Highlights, c.Risks, c.Recommendations, generatedAt, model, fromCache);
}
