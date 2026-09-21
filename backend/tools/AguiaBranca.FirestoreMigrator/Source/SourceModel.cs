namespace AguiaBranca.FirestoreMigrator.Source;

/// <summary>
/// Documento lido da origem, já sem tipos do Firestore: valores são <c>string</c>, <c>long</c>, <c>double</c>, <c>bool</c>,
/// <c>DateTime</c> (UTC, vindo de <c>Timestamp</c>), <c>IDictionary</c>, <c>IList</c> ou <c>null</c>.
/// </summary>
public sealed record SourceDoc(string Id, IReadOnlyDictionary<string, object?> Fields);

/// <summary>Leitura da origem (Firestore). Isolada por interface para testar as transformações sem rede.</summary>
public interface IFirestoreSource
{
    /// <summary>Todos os documentos de uma coleção ou subcoleção (ex.: <c>projects/abc/updates</c>).</summary>
    Task<IReadOnlyList<SourceDoc>> ReadCollectionAsync(string path, CancellationToken ct);
}

/// <summary>Nomes das coleções do Firestore (app da Sprint 1).</summary>
public static class FirestorePaths
{
    public const string Users = "users";
    public const string Guidelines = "strategicGuidelines";
    public const string Ideas = "ideas";
    public const string Projects = "projects";
    public static string ProjectUpdates(string projectId) => $"projects/{projectId}/updates";
}
