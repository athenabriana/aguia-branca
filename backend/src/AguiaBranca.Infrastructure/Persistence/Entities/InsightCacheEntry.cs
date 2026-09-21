using AguiaBranca.Domain.Common;

namespace AguiaBranca.Infrastructure.Persistence.Entities;

/// <summary>Documento de cache dos insights de IA (coleção <c>aiInsights</c>). Detalhe de persistência, não é entidade de domínio.</summary>
public sealed class InsightCacheEntry
{
    public string Id { get; set; } = EntityId.New();
    public string CacheKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Filters { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
