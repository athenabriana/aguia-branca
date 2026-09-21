using System.Globalization;
using AguiaBranca.FirestoreMigrator.Source;

namespace AguiaBranca.FirestoreMigrator.Transform;

/// <summary>Leitor tolerante dos campos de um documento (o Firestore guarda números como long ou double).</summary>
public readonly struct Fields(IReadOnlyDictionary<string, object?> map)
{
    public object? Raw(string name) => map.TryGetValue(name, out var v) ? v : null;

    public string? String(string name) => Raw(name) switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        var other => System.Convert.ToString(other, CultureInfo.InvariantCulture)
    };

    /// <summary>Número como decimal; <c>null</c> se ausente. <c>ok=false</c> se presente mas não numérico.</summary>
    public decimal? Decimal(string name, out bool ok)
    {
        ok = true;
        switch (Raw(name))
        {
            case null: return null;
            case long l: return l;
            case int i: return i;
            case double d when double.IsFinite(d): return (decimal)d;
            case decimal m: return m;
            case string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed): return parsed;
            default: ok = false; return null;
        }
    }

    public DateTime? Date(string name, out bool ok)
    {
        ok = true;
        switch (Raw(name))
        {
            case null: return null;
            case DateTime d: return DateTime.SpecifyKind(d.ToUniversalTime(), DateTimeKind.Utc);
            case DateTimeOffset o: return o.UtcDateTime;
            default: ok = false; return null;
        }
    }

    public Fields? Map(string name) =>
        Raw(name) is IReadOnlyDictionary<string, object?> ro ? new Fields(ro)
        : Raw(name) is IDictionary<string, object?> d ? new Fields(new Dictionary<string, object?>(d))
        : null;

    public IReadOnlyList<object?> List(string name) =>
        Raw(name) is System.Collections.IEnumerable list and not string ? list.Cast<object?>().ToList() : [];

    public static Fields Of(SourceDoc doc) => new(doc.Fields);
}
