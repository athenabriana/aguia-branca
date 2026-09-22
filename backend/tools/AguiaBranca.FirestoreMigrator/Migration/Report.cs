using System.Text.Json;
using System.Text.Json.Serialization;

namespace AguiaBranca.FirestoreMigrator.Migration;

/// <summary>
/// Conciliação de uma coleção. <c>Source</c> = documentos lidos; <c>Invalid</c> = rejeitados na validação;
/// <c>Discarded</c> = conflitos de e-mail e itens sem autor/gestor no destino; <c>Migrated</c> = enviados ao destino.
/// </summary>
public sealed record CollectionReport(
    string Collection, int Source, int Invalid, int Discarded, int Migrated, long? VerifiedInDestination, int OrphanReferences, int Warnings)
{
    /// <summary>Origem = destino, descontando o que foi reportado como inválido/conflito. <c>null</c> em dry-run.</summary>
    [JsonIgnore]
    public bool? Reconciled => VerifiedInDestination is { } v ? v == Migrated && Source - Invalid - Discarded == Migrated : null;
}

public sealed record MigrationReport(
    DateTime StartedAt, bool DryRun, IReadOnlyList<CollectionReport> Collections, IReadOnlyList<Issue> Issues,
    int IntegrityViolations, int GeneratedDerivedRecords)
{
    public bool Reconciled => Collections.All(c => c.Reconciled != false) && IntegrityViolations == 0;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };

    public string ToJson() => JsonSerializer.Serialize(new
    {
        startedAt = StartedAt, dryRun = DryRun, reconciled = Reconciled, integrityViolations = IntegrityViolations,
        collections = Collections.Select(c => new { c.Collection, c.Source, c.Invalid, c.Discarded, c.Migrated, c.VerifiedInDestination, c.OrphanReferences, c.Warnings, reconciled = c.Reconciled }),
        issues = Issues
    }, Json);

    public string ToConsole()
    {
        var w = new System.Text.StringBuilder();
        w.AppendLine(DryRun ? "== RELATÓRIO DE CONCILIAÇÃO (dry-run: nada foi escrito) ==" : "== RELATÓRIO DE CONCILIAÇÃO ==");
        w.AppendLine($"{"coleção",-16}{"origem",8}{"inválidos",11}{"descartados",13}{"migrados",10}{"no destino",12}{"órfãs",7}{"avisos",8}");
        foreach (var c in Collections)
            w.AppendLine($"{c.Collection,-16}{c.Source,8}{c.Invalid,11}{c.Discarded,13}{c.Migrated,10}{(c.VerifiedInDestination?.ToString() ?? "—"),12}{c.OrphanReferences,7}{c.Warnings,8}");
        w.AppendLine($"Violações de integridade referencial: {IntegrityViolations}");
        w.AppendLine($"Registros derivados gerados (histórico CREATED / pontos MIGRATION): {GeneratedDerivedRecords}");
        foreach (var group in Issues.GroupBy(i => i.Kind))
        {
            w.AppendLine($"-- {group.Key} ({group.Count()}) --");
            foreach (var i in group.Take(50)) w.AppendLine($"   [{i.Collection}] {i.LegacyId}: {i.Message}");
            if (group.Count() > 50) w.AppendLine($"   … mais {group.Count() - 50} (veja o relatório JSON)");
        }
        w.AppendLine(Reconciled ? "Conciliação OK." : "ATENÇÃO: há divergências — revise o relatório.");
        return w.ToString();
    }
}
