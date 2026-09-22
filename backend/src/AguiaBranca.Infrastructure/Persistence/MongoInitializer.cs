using AguiaBranca.Infrastructure.Configuration;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Persistence;

/// <summary>Startup: confirma replica set (transações) e garante índices. Falha rápido com mensagem clara.</summary>
internal sealed class MongoInitializer(
    IMongoClient client, IOptions<MongoOptions> options, ILogger<MongoInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.InitializeOnStartup)
        {
            logger.LogInformation("Inicialização do MongoDB desabilitada (Mongo:InitializeOnStartup=false).");
            return;
        }

        if (settings.RequireReplicaSet && !await IsReplicaSetAsync(client, cancellationToken))
            throw new InvalidOperationException(
                "O MongoDB não está em modo replica set. As transações multi-documento exigem replica set: " +
                "use `docker compose up -d mongo` (rs0) ou MongoDB Atlas. " +
                "Para desabilitar esta verificação (não recomendado) defina Mongo:RequireReplicaSet=false.");

        await IndexInitializer.EnsureIndexesAsync(client.GetDatabase(settings.Database), cancellationToken);
        logger.LogInformation("MongoDB pronto: banco {Database}, índices garantidos.", settings.Database);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task<bool> IsReplicaSetAsync(IMongoClient client, CancellationToken ct)
    {
        var hello = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        return hello.Contains("setName");
    }
}
