using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Tests.Support;

public sealed class FakeInsightGenerator : IInsightGenerator
{
    public string Model { get; set; } = "modelo-fake";
    public bool IsConfigured { get; set; } = true;
    public List<InsightRequest> Requests { get; } = [];
    public Func<InsightRequest, Result<GeneratedInsight>>? Behavior { get; set; }

    public int Calls => Requests.Count;

    public static GeneratedInsight DefaultInsight(string? relatedRef = "G1") => new(
        "Resumo executivo dos resultados.", ["Destaque A", "Destaque B"], ["Risco A"],
        [new GeneratedRecommendation("Escalar o piloto", "Detalhe da ação", InsightPriority.ALTA, relatedRef)]);

    public Task<Result<GeneratedInsight>> GenerateAsync(InsightRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(Behavior?.Invoke(request) ?? Result<GeneratedInsight>.Ok(DefaultInsight()));
    }
}

public sealed class FakeInsightCache : IInsightCache
{
    public Dictionary<string, CachedInsight> Items { get; } = [];

    public Task<CachedInsight?> GetAsync(string cacheKey, DateTime now, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue(cacheKey, out var e) && e.ExpiresAt > now ? e : null);

    public Task SetAsync(CachedInsight insight, CancellationToken ct) { Items[insight.CacheKey] = insight; return Task.CompletedTask; }
}

public sealed class FakeInsightQuota : IInsightQuota
{
    private readonly Dictionary<string, int> _used = [];
    public int Consumed => _used.Values.Sum();
    public List<string> Days { get; } = [];

    public Task<bool> TryConsumeAsync(string dayKey, int limit, CancellationToken ct)
    {
        Days.Add(dayKey);
        _used.TryGetValue(dayKey, out var used);
        if (used >= limit) return Task.FromResult(false);
        _used[dayKey] = used + 1;
        return Task.FromResult(true);
    }
}

public sealed class FakeInsightPolicy : IInsightPolicy
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(6);
    public int DailyLimit { get; set; } = 100;
}
