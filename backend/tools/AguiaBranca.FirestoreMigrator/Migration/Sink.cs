using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.FirestoreMigrator.Migration;

public sealed record ExistingUser(ObjectId Id, string? LegacyId);

public enum UpsertMode
{
    /// <summary>Substitui o documento inteiro (upsert por <c>_id</c>).</summary>
    Replace,
    /// <summary>Só <c>$set</c> dos campos informados (usuário já existente: credenciais ficam intactas).</summary>
    SetFields
}

/// <summary>Destino da migração (MongoDB). Isolado por interface para testar o runner sem banco.</summary>
public interface IMigrationSink
{
    Task EnsureIndexesAsync(CancellationToken ct);
    Task<Dictionary<string, ObjectId>> LoadIdMapAsync(CancellationToken ct);
    Task SaveIdMapAsync(IReadOnlyDictionary<string, ObjectId> entries, CancellationToken ct);
    Task<ExistingUser?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct);
    Task UpsertAsync(string collection, ObjectId id, BsonDocument document, UpsertMode mode, CancellationToken ct);
    Task<long> CountByIdsAsync(string collection, IReadOnlyCollection<ObjectId> ids, CancellationToken ct);
}

public sealed class MongoSink(IMongoDatabase db) : IMigrationSink
{
    public const string IdMapCollection = "migration_idmap";

    public static MongoSink Connect(string connectionString, string database) =>
        new(new MongoClient(connectionString).GetDatabase(database));

    public Task EnsureIndexesAsync(CancellationToken ct) => IndexInitializer.EnsureIndexesAsync(db, ct);

    public async Task<Dictionary<string, ObjectId>> LoadIdMapAsync(CancellationToken ct)
    {
        var docs = await db.GetCollection<BsonDocument>(IdMapCollection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return docs.ToDictionary(d => d["_id"].AsString, d => d["newId"].AsObjectId);
    }

    public async Task SaveIdMapAsync(IReadOnlyDictionary<string, ObjectId> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return;
        var models = entries.Select(e => (WriteModel<BsonDocument>)new ReplaceOneModel<BsonDocument>(
            Builders<BsonDocument>.Filter.Eq("_id", e.Key), new BsonDocument { ["_id"] = e.Key, ["newId"] = e.Value }) { IsUpsert = true });
        await db.GetCollection<BsonDocument>(IdMapCollection).BulkWriteAsync(models, cancellationToken: ct);
    }

    public async Task<ExistingUser?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        var doc = await db.GetCollection<BsonDocument>(Collections.Users)
            .Find(Builders<BsonDocument>.Filter.Eq("normalizedEmail", normalizedEmail)).FirstOrDefaultAsync(ct);
        return doc is null ? null : new ExistingUser(doc["_id"].AsObjectId, doc.GetValue("legacyId", BsonNull.Value) is { IsString: true } l ? l.AsString : null);
    }

    public async Task UpsertAsync(string collection, ObjectId id, BsonDocument document, UpsertMode mode, CancellationToken ct)
    {
        var col = db.GetCollection<BsonDocument>(collection);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);

        if (mode == UpsertMode.Replace)
        {
            document["_id"] = id;
            await col.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, ct);
        }
        else
        {
            var update = Builders<BsonDocument>.Update.Combine(document.Elements.Select(e => Builders<BsonDocument>.Update.Set(e.Name, e.Value)));
            await col.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }

    public Task<long> CountByIdsAsync(string collection, IReadOnlyCollection<ObjectId> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(0L)
            : db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(Builders<BsonDocument>.Filter.In("_id", ids), cancellationToken: ct);
}
