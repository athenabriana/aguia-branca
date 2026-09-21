using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguiaBranca.Infrastructure.Persistence.Repositories;

internal sealed class InsightCache(AppDbContext db) : IInsightCache
{
    public async Task<CachedInsight?> GetAsync(string cacheKey, DateTime now, CancellationToken ct)
    {
        var entry = await db.AiInsights.AsNoTracking().FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);
        return entry is null || entry.ExpiresAt <= now ? null : ToModel(entry);
    }

    public async Task SetAsync(CachedInsight insight, CancellationToken ct)
    {
        // A chave é única: substitui a entrada existente (expirada e ainda não removida pelo TTL, ou "refresh").
        var existing = await db.AiInsights.FirstOrDefaultAsync(x => x.CacheKey == insight.CacheKey, ct);
        if (existing is null)
        {
            await db.AiInsights.AddAsync(new InsightCacheEntry
            {
                CacheKey = insight.CacheKey, UserId = insight.UserId, Filters = insight.Filters, Model = insight.Model,
                PayloadJson = insight.PayloadJson, CreatedAt = insight.CreatedAt, ExpiresAt = insight.ExpiresAt
            }, ct);
            return;
        }

        existing.UserId = insight.UserId;
        existing.Filters = insight.Filters;
        existing.Model = insight.Model;
        existing.PayloadJson = insight.PayloadJson;
        existing.CreatedAt = insight.CreatedAt;
        existing.ExpiresAt = insight.ExpiresAt;
    }

    private static CachedInsight ToModel(InsightCacheEntry e) =>
        new(e.CacheKey, e.UserId, e.Filters, e.Model, e.PayloadJson, e.CreatedAt, e.ExpiresAt);
}
