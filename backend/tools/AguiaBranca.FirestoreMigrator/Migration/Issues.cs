namespace AguiaBranca.FirestoreMigrator.Migration;

public enum IssueKind
{
    /// <summary>Item descartado (campo obrigatório ausente, enum desconhecido, valor não numérico…).</summary>
    Invalid,
    /// <summary>Referência a um ID que não existe no destino: opcional vira nula; obrigatória descarta o item.</summary>
    OrphanReference,
    /// <summary>Item migrado com ajuste (ex.: data ausente substituída, ICE fora da faixa descartado).</summary>
    Warning,
    /// <summary>Conflito com dados já existentes no destino (ex.: e-mail já usado por outro usuário).</summary>
    Conflict
}

public sealed record Issue(string Collection, string LegacyId, IssueKind Kind, string Message);

public sealed class IssueLog
{
    private readonly List<Issue> _items = [];
    public IReadOnlyList<Issue> Items => _items;

    public void Add(string collection, string legacyId, IssueKind kind, string message) => _items.Add(new Issue(collection, legacyId, kind, message));
    public int Count(string collection, IssueKind kind) => _items.Count(i => i.Collection == collection && i.Kind == kind);
}
