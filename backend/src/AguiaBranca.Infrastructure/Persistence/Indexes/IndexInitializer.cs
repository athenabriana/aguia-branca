using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Persistence.Indexes;

/// <summary>
/// Cria índices, únicos e TTLs (o provider EF não gerencia índices nem migrations). Idempotente:
/// nomes explícitos + mesma especificação = no-op. Ver spec → "Índices".
/// </summary>
public static class IndexInitializer
{
    public static readonly TimeSpan RefreshTokenRetentionAfterExpiry = TimeSpan.FromDays(1);
    /// <summary>Os contadores diários de IA só importam no próprio dia; sobram 2 dias para fusos e depuração.</summary>
    public static readonly TimeSpan AiUsageRetention = TimeSpan.FromDays(2);

    private static IndexKeysDefinitionBuilder<BsonDocument> Keys => Builders<BsonDocument>.IndexKeys;

    public static async Task EnsureIndexesAsync(IMongoDatabase db, CancellationToken ct = default)
    {
        var users = db.GetCollection<BsonDocument>(Collections.Users);
        await users.Indexes.CreateManyAsync(
        [
            Unique("ux_users_normalizedEmail", Keys.Ascending("normalizedEmail"), PartialString("normalizedEmail")),
            Unique("ux_users_normalizedUserName", Keys.Ascending("normalizedUserName"), PartialString("normalizedUserName")),
            Plain("ix_users_role", Keys.Ascending("role"))
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.RefreshTokens).Indexes.CreateManyAsync(
        [
            Unique("ux_refreshTokens_tokenHash", Keys.Ascending("tokenHash")),
            Plain("ix_refreshTokens_familyId", Keys.Ascending("familyId")),
            new CreateIndexModel<BsonDocument>(Keys.Ascending("expiresAt"),
                new CreateIndexOptions { Name = "ttl_refreshTokens_expiresAt", ExpireAfter = RefreshTokenRetentionAfterExpiry })
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.Guidelines).Indexes.CreateManyAsync(
            [Plain("ix_guidelines_updatedAt", Keys.Descending("updatedAt"))], ct);

        await db.GetCollection<BsonDocument>(Collections.GuidelineHistory).Indexes.CreateManyAsync(
        [
            Plain("ix_guidelineHistory_guidelineId_occurredAt", Keys.Ascending("guidelineId").Descending("occurredAt")),
            Plain("ix_guidelineHistory_category_occurredAt", Keys.Ascending("category").Descending("occurredAt")),
            Plain("ix_guidelineHistory_campaign_occurredAt", Keys.Ascending("campaign").Descending("occurredAt"))
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.Ideas).Indexes.CreateManyAsync(
        [
            Plain("ix_ideas_authorId_createdAt", Keys.Ascending("authorId").Descending("createdAt")),
            Plain("ix_ideas_status_iceScore", Keys.Ascending("status").Descending("ice.score")),
            Plain("ix_ideas_guidelineId_createdAt", Keys.Ascending("guidelineId").Descending("createdAt"))
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.Projects).Indexes.CreateManyAsync(
        [
            Plain("ix_projects_division_updatedAt", Keys.Ascending("division").Descending("updatedAt")),
            Plain("ix_projects_guidelineId_updatedAt", Keys.Ascending("guidelineId").Descending("updatedAt")),
            Plain("ix_projects_stage_updatedAt", Keys.Ascending("stage").Descending("updatedAt")),
            // Um projeto por ideia de origem: base da idempotência de "aprovar ideia" (R2-03.8).
            Unique("ux_projects_originatingIdeaId", Keys.Ascending("originatingIdeaId"),
                new BsonDocument("originatingIdeaId", new BsonDocument("$type", "objectId")))
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.ProjectUpdates).Indexes.CreateManyAsync(
            [Plain("ix_projectUpdates_projectId_createdAt", Keys.Ascending("projectId").Descending("createdAt"))], ct);

        await db.GetCollection<BsonDocument>(Collections.PointEvents).Indexes.CreateManyAsync(
        [
            Plain("ix_pointEvents_userId_createdAt", Keys.Ascending("userId").Ascending("createdAt")),
            Plain("ix_pointEvents_createdAt", Keys.Ascending("createdAt"))
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.AiInsights).Indexes.CreateManyAsync(
        [
            Unique("ux_aiInsights_cacheKey", Keys.Ascending("cacheKey")),
            new CreateIndexModel<BsonDocument>(Keys.Ascending("expiresAt"),
                new CreateIndexOptions { Name = "ttl_aiInsights_expiresAt", ExpireAfter = TimeSpan.Zero })
        ], ct);

        await db.GetCollection<BsonDocument>(Collections.AiUsage).Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Keys.Ascending("expiresAt"),
                new CreateIndexOptions { Name = "ttl_aiUsage_expiresAt", ExpireAfter = TimeSpan.Zero })
        ], ct);
    }

    private static BsonDocument PartialString(string field) => new(field, new BsonDocument("$type", "string"));

    private static CreateIndexModel<BsonDocument> Plain(string name, IndexKeysDefinition<BsonDocument> keys) =>
        new(keys, new CreateIndexOptions { Name = name });

    private static CreateIndexModel<BsonDocument> Unique(
        string name, IndexKeysDefinition<BsonDocument> keys, BsonDocument? partialFilter = null) =>
        new(keys, new CreateIndexOptions<BsonDocument>
        {
            Name = name,
            Unique = true,
            PartialFilterExpression = partialFilter
        });
}
