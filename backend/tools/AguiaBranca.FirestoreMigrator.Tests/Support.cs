using AguiaBranca.FirestoreMigrator.Migration;
using AguiaBranca.FirestoreMigrator.Source;
using MongoDB.Bson;

namespace AguiaBranca.FirestoreMigrator.Tests;

public sealed class InMemorySource : IFirestoreSource
{
    private readonly Dictionary<string, List<SourceDoc>> _collections = [];

    public InMemorySource Add(string path, string id, Dictionary<string, object?> fields)
    {
        if (!_collections.TryGetValue(path, out var list)) _collections[path] = list = [];
        list.Add(new SourceDoc(id, fields));
        return this;
    }

    public Task<IReadOnlyList<SourceDoc>> ReadCollectionAsync(string path, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SourceDoc>>(_collections.TryGetValue(path, out var l) ? l : []);
}

/// <summary>Destino em memória com a mesma semântica de upsert do Mongo (por _id).</summary>
public sealed class InMemorySink : IMigrationSink
{
    public Dictionary<string, Dictionary<ObjectId, BsonDocument>> Data { get; } = [];
    public Dictionary<string, ObjectId> IdMap { get; } = [];
    public List<string> Calls { get; } = [];

    public IEnumerable<BsonDocument> Docs(string collection) => Data.TryGetValue(collection, out var d) ? d.Values : [];
    public int Total => Data.Values.Sum(d => d.Count);

    public Task EnsureIndexesAsync(CancellationToken ct) { Calls.Add("indexes"); return Task.CompletedTask; }
    public Task<Dictionary<string, ObjectId>> LoadIdMapAsync(CancellationToken ct) => Task.FromResult(new Dictionary<string, ObjectId>(IdMap));

    public Task SaveIdMapAsync(IReadOnlyDictionary<string, ObjectId> entries, CancellationToken ct)
    {
        Calls.Add("idmap");
        foreach (var (k, v) in entries) IdMap[k] = v;
        return Task.CompletedTask;
    }

    public Task<ExistingUser?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        var doc = Docs("users").FirstOrDefault(d => d["normalizedEmail"].AsString == normalizedEmail);
        return Task.FromResult(doc is null ? null : new ExistingUser(doc["_id"].AsObjectId, doc.GetValue("legacyId", BsonNull.Value) is { IsString: true } l ? l.AsString : null));
    }

    public Task UpsertAsync(string collection, ObjectId id, BsonDocument document, UpsertMode mode, CancellationToken ct)
    {
        Calls.Add(collection);
        if (!Data.TryGetValue(collection, out var docs)) Data[collection] = docs = [];
        if (mode == UpsertMode.Replace) { var copy = document.DeepClone().AsBsonDocument; copy["_id"] = id; docs[id] = copy; }
        else foreach (var e in document.Elements) docs[id][e.Name] = e.Value;
        return Task.CompletedTask;
    }

    public Task<long> CountByIdsAsync(string collection, IReadOnlyCollection<ObjectId> ids, CancellationToken ct) =>
        Task.FromResult((long)ids.Count(i => Data.TryGetValue(collection, out var d) && d.ContainsKey(i)));
}

/// <summary>Dataset fictício no formato dos DTOs do app (campos e defaults do Firestore).</summary>
public static class Sample
{
    public static readonly DateTime Now = new(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc);
    public static DateTime T(int m, int d, int h = 12) => new(2026, m, d, h, 0, 0, DateTimeKind.Utc);

    public static Dictionary<string, object?> User(string name, string email, string role = "OPERADOR", long points = 0, object? createdAt = null, string? division = "LOGISTICA") =>
        new() { ["name"] = name, ["email"] = email, ["role"] = role, ["division"] = division, ["points"] = points, ["badges"] = new List<object?>(), ["createdAt"] = createdAt ?? T(1, 10) };

    public static Dictionary<string, object?> Guideline(string title, string author, string pillar = "IDEIAS") =>
        new() { ["title"] = title, ["description"] = "Descrição", ["pillar"] = pillar, ["authorId"] = author, ["authorName"] = "Líder", ["createdAt"] = T(2, 1), ["updatedAt"] = T(2, 2) };

    public static Dictionary<string, object?> Idea(string title, string author, string status = "SUBMETIDA", string? guideline = null, object? ice = null, object? createdAt = null) =>
        new()
        {
            ["title"] = title, ["description"] = "d", ["category"] = "Tecnologia", ["division"] = "LOGISTICA", ["guidelineId"] = guideline,
            ["authorId"] = author, ["authorName"] = "Autor", ["status"] = status, ["ice"] = ice, ["reviewerId"] = null, ["reviewComment"] = null,
            ["createdAt"] = createdAt ?? T(3, 1), ["reviewedAt"] = null
        };

    public static Dictionary<string, object?> Project(string title, string creator, string stage = "EM_EXECUCAO", string? guideline = null, string? idea = null,
        object? investment = null, object? financialReturn = null) =>
        new()
        {
            ["title"] = title, ["description"] = "d", ["stage"] = stage, ["statusText"] = "ok", ["investment"] = investment ?? 1000.0, ["targetDate"] = T(12, 1),
            ["financialReturn"] = financialReturn ?? 2500L, ["productivityGain"] = 10.5, ["costReduction"] = 300L, ["division"] = "LOGISTICA",
            ["guidelineId"] = guideline, ["creatorManagerId"] = creator, ["originatingIdeaId"] = idea, ["priorityScore"] = 648L,
            ["reporterId"] = null, ["reporterName"] = null, ["responsibleId"] = creator, ["responsibleName"] = "Gestor",
            ["createdAt"] = T(4, 1), ["updatedAt"] = T(5, 1)
        };

    public static Dictionary<string, object?> Update(string author, string note = "Ajuste", List<object?>? changes = null) =>
        new() { ["authorId"] = author, ["authorName"] = "Gestor", ["note"] = note, ["changes"] = changes ?? [], ["createdAt"] = T(5, 2) };

    /// <summary>Cenário completo e consistente: 3 usuários, 2 orientações, 3 ideias, 2 projetos (1 concluído) e 2 atualizações.</summary>
    public static InMemorySource Full()
    {
        var s = new InMemorySource();
        s.Add("users", "u-lider", User("Líder", "lider@x.com", "LIDER", 0, division: "CORPORATIVO"));
        s.Add("users", "u-gestor", User("Gestor", "gestor@x.com", "GESTOR", 0));
        s.Add("users", "u-op", User("Operadora", "op@x.com", "OPERADOR", 265));
        s.Add("strategicGuidelines", "g1", Guideline("Eficiência", "u-lider"));
        s.Add("strategicGuidelines", "g2", Guideline("Cliente", "u-lider", "DIRECIONAMENTO"));
        s.Add("ideas", "i1", Idea("Ideia implementada", "u-op", "IMPLEMENTADA", "g1", new Dictionary<string, object?> { ["impact"] = 9L, ["confidence"] = 9L, ["ease"] = 8L, ["score"] = 648L }));
        s.Add("ideas", "i2", Idea("Ideia submetida", "u-op"));
        s.Add("ideas", "i3", Idea("Ideia rejeitada", "u-op", "REJEITADA", "g2"));
        s.Add("projects", "p1", Project("PROJ: Ideia implementada", "u-gestor", "CONCLUIDO", "g1", "i1"));
        s.Add("projects", "p2", Project("Projeto avulso", "u-gestor", "PLANEJAMENTO", null, null, 0L, 0L));
        s.Add("projects/p1/updates", "up1", Update("u-gestor", "Concluído", [new Dictionary<string, object?> { ["field"] = "stage", ["from"] = "EM_EXECUCAO", ["to"] = "CONCLUIDO" }]));
        s.Add("projects/p2/updates", "up2", Update("u-gestor", "Criado"));
        return s;
    }

    public static MigrationOptions Options(bool dryRun = false) =>
        new(dryRun, dryRun ? null : "senha-temporaria-1", Now, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
}
