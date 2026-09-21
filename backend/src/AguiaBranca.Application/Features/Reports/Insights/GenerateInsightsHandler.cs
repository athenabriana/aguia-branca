using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;

namespace AguiaBranca.Application.Features.Reports.Insights;

/// <summary>
/// Insights de IA sobre o dashboard: resumo (B17) → prompt sem PII → cache (6 h, por digest dos dados) → teto diário →
/// Gemini → validação → grava no cache. Falha da IA nunca vira insight inventado e nunca é cacheada.
/// </summary>
public sealed class GenerateInsightsHandler(
    IValidationService validation, IIdeaRepository ideas, IProjectRepository projects, IGuidelineRepository guidelines,
    IInsightGenerator generator, IInsightCache cache, IInsightQuota quota, IInsightPolicy policy,
    IUnitOfWork uow, ICurrentUser currentUser, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<GenerateInsightsCommand, InsightResponse>
{
    private static readonly JsonSerializerOptions CacheJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<Result<InsightResponse>> HandleAsync(GenerateInsightsCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<InsightResponse>.Fail(errors);

        var now = clock.UtcNow;
        var tz = timeZone.ReportTimeZone;

        var (guidelineList, ideaList, projectList, focusTitle) = await LoadScopeAsync(request, ct);
        if (guidelineList is null) return ReportErrors.GuidelineNotFound;

        var summary = ReportCalculator.Compute(ideaList, projectList, guidelineList, new ReportFilters(request.Period, request.Division), now, tz);
        var prompt = InsightPromptBuilder.Build(summary, focusTitle);

        var model = generator.Model;
        var cacheKey = CacheKey(model, request, prompt.Payload);

        if (!request.Refresh && policy.CacheDuration > TimeSpan.Zero
            && await cache.GetAsync(cacheKey, now, ct) is { } hit && TryReadContent(hit.PayloadJson) is { } cached)
            return InsightResponse.From(cached, hit.CreatedAt, hit.Model, fromCache: true);

        if (!generator.IsConfigured) return AiErrors.Unavailable("A IA não está configurada neste ambiente.");

        var day = ReportCalculator.TodayIn(now, tz).ToString("yyyy-MM-dd");
        if (!await quota.TryConsumeAsync(day, policy.DailyLimit, ct))
            return new Error("RATE_LIMITED", "O limite diário de gerações de insights foi atingido. Tente novamente amanhã.",
                ErrorType.TooManyRequests, RetryAfterSeconds: SecondsUntilNextLocalDay(now, tz));

        var generated = await generator.GenerateAsync(prompt.Request, ct);
        if (generated.IsFailure) return Result<InsightResponse>.Fail(generated.Errors);

        var content = ToContent(generated.Value, prompt.GuidelineRefs);
        await StoreAsync(cacheKey, request, model, content, now, ct);

        return InsightResponse.From(content, now, model, fromCache: false);
    }

    /// <summary>Com <c>guidelineId</c>, o resumo cobre só aquela orientação (ideias, projetos e a própria orientação).</summary>
    private async Task<(IReadOnlyList<Domain.Entities.Guideline>? Guidelines, IReadOnlyList<Domain.Entities.Idea> Ideas,
        IReadOnlyList<Domain.Entities.Project> Projects, string? FocusTitle)> LoadScopeAsync(GenerateInsightsCommand request, CancellationToken ct)
    {
        var allIdeas = await ideas.ListAllAsync(request.Division, ct);
        var allProjects = await projects.ListAllAsync(request.Division, ct);

        if (request.GuidelineId is null)
            return (await guidelines.ListAsync(ct), allIdeas, allProjects, null);

        var guideline = await guidelines.GetByIdAsync(request.GuidelineId, ct);
        return guideline is null
            ? (null, allIdeas, allProjects, null)
            : ([guideline], allIdeas.Where(i => i.GuidelineId == guideline.Id).ToList(),
               allProjects.Where(p => p.GuidelineId == guideline.Id).ToList(), guideline.Title);
    }

    private static InsightContent ToContent(GeneratedInsight generated, IReadOnlyDictionary<string, string> guidelineRefs) =>
        new(generated.Summary, generated.Highlights, generated.Risks,
            generated.Recommendations.Select(r => new InsightRecommendation(
                r.Title, r.Detail, r.Priority,
                // Referência desconhecida (o modelo inventou "G9") vira nulo: nunca repassamos um vínculo falso.
                r.RelatedGuidelineRef is { } reference && guidelineRefs.TryGetValue(reference.Trim().ToUpperInvariant(), out var id) ? id : null))
                .ToList());

    private async Task StoreAsync(string cacheKey, GenerateInsightsCommand request, string model, InsightContent content, DateTime now, CancellationToken ct)
    {
        if (policy.CacheDuration <= TimeSpan.Zero) return;

        await cache.SetAsync(new CachedInsight(
            cacheKey, currentUser.Id, $"{request.Period}|{request.Division}|{request.GuidelineId}", model,
            JsonSerializer.Serialize(content, CacheJson), now, now + policy.CacheDuration), ct);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DuplicateKeyException or ConcurrencyConflictException)
        {
            // Outra requisição idêntica gravou a mesma chave ao mesmo tempo: o insight já está em cache. Segue com o gerado.
        }
    }

    private static InsightContent? TryReadContent(string json)
    {
        try { return JsonSerializer.Deserialize<InsightContent>(json, CacheJson); }
        catch (JsonException) { return null; } // entrada corrompida = cache miss
    }

    /// <summary>Muda com filtros, modelo <b>e dados</b> (o payload carrega os agregados): editar um projeto invalida o cache.</summary>
    private static string CacheKey(string model, GenerateInsightsCommand request, string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{model}|{request.Period}|{request.Division}|{request.GuidelineId}|{payload}"))).ToLowerInvariant();

    private static int SecondsUntilNextLocalDay(DateTime nowUtc, TimeZoneInfo tz) =>
        Math.Max(1, (int)Math.Ceiling((ReportCalculator.StartOfLocalDayUtc(nowUtc, tz).AddDays(1) - nowUtc).TotalSeconds));
}
