using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Ai;

/// <summary>
/// Contador diário atômico de gerações: um documento por dia. O incremento condicional
/// (<c>count &lt; limite</c>) é uma única operação no servidor — várias requisições simultâneas nunca passam do teto.
/// Fora do EF/transações de propósito (a reserva não deve ser desfeita por rollback de outra operação).
/// </summary>
internal sealed class MongoInsightQuota(IMongoDatabase database, IClock clock) : IInsightQuota
{
    public async Task<bool> TryConsumeAsync(string dayKey, int limit, CancellationToken ct)
    {
        var collection = database.GetCollection<BsonDocument>(Collections.AiUsage);

        // Sem documento (ou com contagem abaixo do teto) o filtro casa/insere; no teto o filtro não casa e o upsert
        // tenta inserir o mesmo _id → chave duplicada = teto atingido.
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", dayKey),
            Builders<BsonDocument>.Filter.Lt("count", limit));
        var update = Builders<BsonDocument>.Update
            .Inc("count", 1)
            .SetOnInsert("expiresAt", clock.UtcNow + IndexInitializer.AiUsageRetention);

        try
        {
            await collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true }, ct);
            return true;
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            return false;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
