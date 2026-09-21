using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Indexes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using Testcontainers.MongoDb;

namespace AguiaBranca.Infrastructure.Tests.Support;

/// <summary>
/// MongoDB real em replica set (Testcontainers, compartilhado por processo). Para um ciclo local rápido, defina
/// <c>AGUIA_TEST_MONGO=mongodb://localhost:27017/?directConnection=true</c> e use o Mongo do docker compose
/// (cada teste usa um banco próprio, removido ao final).
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    public IMongoClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        (Client, ConnectionString) = await SharedMongo.GetAsync();
    }

    /// <summary>O container é compartilhado pelo processo de testes (encerrado no fim, com o Ryuk como garantia).</summary>
    public Task DisposeAsync() => Task.CompletedTask;

    public async Task<TestDatabase> CreateDatabaseAsync(bool withIndexes = true)
    {
        var db = new TestDatabase(Client, "it_" + Guid.NewGuid().ToString("N")[..12]);
        if (withIndexes) await IndexInitializer.EnsureIndexesAsync(db.Database);
        return db;
    }
}

public sealed class TestDatabase(IMongoClient client, string name) : IAsyncDisposable
{
    public IMongoClient Client { get; } = client;
    public string Name { get; } = name;
    public IMongoDatabase Database => Client.GetDatabase(Name);

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseMongoDB(Client, Name)
            // Cada teste usa um banco distinto (=> um service provider por banco); em produção há um só.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options);

    internal MongoUnitOfWork CreateUnitOfWork(AppDbContext context) =>
        new(context, NullLogger<MongoUnitOfWork>.Instance);

    public IMongoCollection<MongoDB.Bson.BsonDocument> Raw(string collection) =>
        Database.GetCollection<MongoDB.Bson.BsonDocument>(collection);

    public async ValueTask DisposeAsync() => await Client.DropDatabaseAsync(Name);
}

/// <summary>Uma única instância do MongoDB por processo de testes (subir um container por classe seria lento).</summary>
internal static class SharedMongo
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static (IMongoClient Client, string ConnectionString)? _instance;
    private static MongoDbContainer? _container;

    public static async Task<(IMongoClient Client, string ConnectionString)> GetAsync()
    {
        if (_instance is { } ready) return ready;

        await Gate.WaitAsync();
        try
        {
            if (_instance is { } again) return again;

            string connectionString;
            var external = Environment.GetEnvironmentVariable("AGUIA_TEST_MONGO");
            if (!string.IsNullOrWhiteSpace(external))
            {
                connectionString = external;
            }
            else
            {
                _container = new MongoDbBuilder("mongo:7").WithReplicaSet().Build();
                await _container.StartAsync();
                connectionString = _container.GetConnectionString();
                AppDomain.CurrentDomain.ProcessExit += (_, _) => _container.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
            }

            _instance = (new MongoClient(connectionString), connectionString);
            return _instance.Value;
        }
        finally
        {
            Gate.Release();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "mongo";
}
