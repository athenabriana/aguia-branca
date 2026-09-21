using Google.Cloud.Firestore;

namespace AguiaBranca.FirestoreMigrator.Source;

/// <summary>
/// Origem real. Autenticação: service account (<c>--credentials</c>) ou, para o emulador, a variável
/// <c>FIRESTORE_EMULATOR_HOST</c> (o SDK a respeita sozinho e dispensa credenciais).
/// </summary>
public sealed class FirestoreSource(FirestoreDb db) : IFirestoreSource
{
    public static async Task<FirestoreSource> ConnectAsync(string projectId, string? credentialsPath)
    {
        var builder = new FirestoreDbBuilder { ProjectId = projectId };
        if (!string.IsNullOrWhiteSpace(credentialsPath))
        {
            builder.CredentialsPath = credentialsPath;
            builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.None;
        }
        else
        {
            builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
        }
        return new FirestoreSource(await builder.BuildAsync());
    }

    public async Task<IReadOnlyList<SourceDoc>> ReadCollectionAsync(string path, CancellationToken ct)
    {
        var snapshot = await db.Collection(path).GetSnapshotAsync(ct);
        return snapshot.Documents
            .Select(d => new SourceDoc(d.Id, d.ToDictionary().ToDictionary(kv => kv.Key, kv => Convert(kv.Value))))
            .ToList();
    }

    /// <summary>Timestamp → DateTime UTC; mapas e listas recursivamente; o resto passa como veio.</summary>
    internal static object? Convert(object? value) => value switch
    {
        Timestamp t => t.ToDateTime(),
        IDictionary<string, object> map => map.ToDictionary(kv => kv.Key, kv => Convert(kv.Value)),
        IEnumerable<object> list when value is not string => list.Select(Convert).ToList(),
        _ => value
    };
}
