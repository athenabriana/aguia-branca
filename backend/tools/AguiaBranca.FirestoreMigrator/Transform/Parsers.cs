using System.Globalization;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;
using AguiaBranca.FirestoreMigrator.Migration;
using AguiaBranca.FirestoreMigrator.Source;

namespace AguiaBranca.FirestoreMigrator.Transform;

/// <summary>
/// Firestore → registros tipados (funções puras). Regras: campo ausente assume o mesmo padrão dos DTOs do app;
/// enum <b>desconhecido</b>, número inválido ou campo obrigatório ausente descartam o item <b>e o registram</b> (a migração
/// continua); datas ausentes assumem <paramref name="now"/> com aviso.
/// </summary>
public static class Parsers
{
    public const string UsersCollection = "users", GuidelinesCollection = "guidelines", IdeasCollection = "ideas",
        ProjectsCollection = "projects", UpdatesCollection = "projectUpdates";

    public static UserRec? User(SourceDoc doc, DateTime now, IssueLog log)
    {
        var f = Fields.Of(doc);
        var email = f.String("email");
        if (email is null || !email.Contains('@')) return Invalid<UserRec>(log, UsersCollection, doc, "e-mail ausente ou inválido");

        if (!Enum_(f, "role", Role.OPERADOR, out Role role, log, UsersCollection, doc)
            || !Enum_(f, "division", Division.CORPORATIVO, out Division division, log, UsersCollection, doc)) return null;

        var points = f.Decimal("points", out var okPoints);
        if (!okPoints) return Invalid<UserRec>(log, UsersCollection, doc, "'points' não é numérico");
        var createdAt = DateOrNow(f, "createdAt", now, log, UsersCollection, doc, out var okDate);
        if (!okDate) return null;

        return new UserRec(doc.Id, f.String("name") ?? email.Split('@')[0], email, role, division,
            (int)Math.Max(0, Math.Truncate(points ?? 0)), createdAt);
    }

    public static GuidelineRec? Guideline(SourceDoc doc, DateTime now, IssueLog log)
    {
        var f = Fields.Of(doc);
        var title = f.String("title");
        if (title is null) return Invalid<GuidelineRec>(log, GuidelinesCollection, doc, "título ausente");
        var authorId = f.String("authorId");
        if (authorId is null) return Invalid<GuidelineRec>(log, GuidelinesCollection, doc, "authorId ausente");
        if (!Enum_(f, "pillar", Pillar.DIRECIONAMENTO, out Pillar pillar, log, GuidelinesCollection, doc)) return null;

        var createdAt = DateOrNow(f, "createdAt", now, log, GuidelinesCollection, doc, out var ok1);
        var updatedAt = f.Date("updatedAt", out var ok2) ?? createdAt;
        if (!ok1 || !ok2) return Invalid<GuidelineRec>(log, GuidelinesCollection, doc, "data inválida");

        return new GuidelineRec(doc.Id, title, f.String("description") ?? string.Empty, pillar, authorId, f.String("authorName") ?? "—", createdAt, updatedAt);
    }

    public static IdeaRec? Idea(SourceDoc doc, DateTime now, IssueLog log)
    {
        var f = Fields.Of(doc);
        var title = f.String("title");
        if (title is null) return Invalid<IdeaRec>(log, IdeasCollection, doc, "título ausente");
        var authorId = f.String("authorId");
        if (authorId is null) return Invalid<IdeaRec>(log, IdeasCollection, doc, "authorId ausente");

        if (!Enum_(f, "division", Division.CORPORATIVO, out Division division, log, IdeasCollection, doc)
            || !Enum_(f, "status", IdeaStatus.SUBMETIDA, out IdeaStatus status, log, IdeasCollection, doc)) return null;

        var createdAt = DateOrNow(f, "createdAt", now, log, IdeasCollection, doc, out var ok1);
        var reviewedAt = f.Date("reviewedAt", out var ok2);
        if (!ok1 || !ok2) return Invalid<IdeaRec>(log, IdeasCollection, doc, "data inválida");

        return new IdeaRec(doc.Id, title, f.String("description") ?? string.Empty, f.String("category") ?? "Geral", division,
            f.String("guidelineId"), authorId, f.String("authorName") ?? "—", status, Ice(f, doc, log),
            f.String("reviewerId"), f.String("reviewComment"), createdAt, reviewedAt, reviewedAt ?? createdAt);
    }

    /// <summary>ICE fora de 1–10 ou não numérico é descartado (a ideia migra sem ICE) — nunca grava valor inválido.</summary>
    private static IceRec? Ice(Fields f, SourceDoc doc, IssueLog log)
    {
        if (f.Map("ice") is not { } ice) return null;
        var impact = ice.Decimal("impact", out var o1);
        var confidence = ice.Decimal("confidence", out var o2);
        var ease = ice.Decimal("ease", out var o3);

        if (!o1 || !o2 || !o3 || impact is null || confidence is null || ease is null
            || !IsInt(impact.Value) || !IsInt(confidence.Value) || !IsInt(ease.Value)
            || !Domain.ValueObjects.Ice.IsValid((int)impact.Value, (int)confidence.Value, (int)ease.Value))
        {
            log.Add(IdeasCollection, doc.Id, IssueKind.Warning, "ICE inválido (esperado inteiros de 1 a 10): descartado");
            return null;
        }
        return new IceRec((int)impact.Value, (int)confidence.Value, (int)ease.Value);
    }

    public static ProjectRec? Project(SourceDoc doc, DateTime now, IssueLog log)
    {
        var f = Fields.Of(doc);
        var title = f.String("title");
        if (title is null) return Invalid<ProjectRec>(log, ProjectsCollection, doc, "título ausente");
        var creator = f.String("creatorManagerId");
        if (creator is null) return Invalid<ProjectRec>(log, ProjectsCollection, doc, "creatorManagerId ausente");

        if (!Enum_(f, "stage", ProjectStage.PLANEJAMENTO, out ProjectStage stage, log, ProjectsCollection, doc)
            || !Enum_(f, "division", Division.CORPORATIVO, out Division division, log, ProjectsCollection, doc)) return null;

        var money = new decimal[4];
        var names = new[] { "investment", "financialReturn", "productivityGain", "costReduction" };
        for (var i = 0; i < names.Length; i++)
        {
            var v = f.Decimal(names[i], out var ok);
            if (!ok) return Invalid<ProjectRec>(log, ProjectsCollection, doc, $"'{names[i]}' não é numérico");
            if (v < 0) return Invalid<ProjectRec>(log, ProjectsCollection, doc, $"'{names[i]}' negativo");
            money[i] = v ?? 0m;
        }

        var priority = f.Decimal("priorityScore", out var okPriority);
        if (!okPriority) return Invalid<ProjectRec>(log, ProjectsCollection, doc, "'priorityScore' não é numérico");

        var createdAt = DateOrNow(f, "createdAt", now, log, ProjectsCollection, doc, out var ok1);
        var updatedAt = f.Date("updatedAt", out var ok2) ?? createdAt;
        var target = f.Date("targetDate", out var ok3);
        if (!ok1 || !ok2 || !ok3) return Invalid<ProjectRec>(log, ProjectsCollection, doc, "data inválida");

        return new ProjectRec(doc.Id, title, f.String("description") ?? string.Empty, stage, f.String("statusText") ?? string.Empty,
            money[0], target, money[1], money[2], money[3], division, f.String("guidelineId"), creator, f.String("originatingIdeaId"),
            priority is null ? null : (int)priority.Value, f.String("reporterId"), f.String("reporterName"),
            f.String("responsibleId"), f.String("responsibleName"), createdAt, updatedAt);
    }

    public static UpdateRec? Update(string projectLegacyId, SourceDoc doc, DateTime now, IssueLog log)
    {
        var f = Fields.Of(doc);
        var authorId = f.String("authorId");
        if (authorId is null) return Invalid<UpdateRec>(log, UpdatesCollection, doc, "authorId ausente");
        var createdAt = DateOrNow(f, "createdAt", now, log, UpdatesCollection, doc, out var ok);
        if (!ok) return null;

        var changes = new List<ChangeRec>();
        foreach (var raw in f.List("changes"))
        {
            if (raw is not IDictionary<string, object?> map) continue;
            var c = new Fields(new Dictionary<string, object?>(map));
            if (c.String("field") is { } field) changes.Add(Change(field, c.Raw("from"), c.Raw("to")));
        }

        return new UpdateRec(doc.Id, projectLegacyId, authorId, f.String("authorName") ?? "—", f.String("note") ?? string.Empty, changes, createdAt);
    }

    /// <summary>Número → NUMBER (forma canônica), data → DATE (ISO UTC), o resto → TEXT — o mesmo formato do servidor.</summary>
    public static ChangeRec Change(string field, object? from, object? to)
    {
        var kind = Kind(from) is { } kf ? kf : Kind(to) ?? FieldValueKind.TEXT;
        return new ChangeRec(field, kind, Render(from, kind), Render(to, kind));
    }

    private static FieldValueKind? Kind(object? v) => v switch
    {
        long or int or double or decimal => FieldValueKind.NUMBER,
        DateTime or DateTimeOffset => FieldValueKind.DATE,
        null => null,
        _ => FieldValueKind.TEXT
    };

    private static string? Render(object? v, FieldValueKind kind) => v switch
    {
        null => null,
        long l => FieldChange.FormatNumber(l),
        int i => FieldChange.FormatNumber(i),
        double d when double.IsFinite(d) => FieldChange.FormatNumber((decimal)d),
        decimal m => FieldChange.FormatNumber(m),
        DateTime dt => FieldChange.FormatDate(DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc)),
        DateTimeOffset o => FieldChange.FormatDate(o.UtcDateTime),
        var other => Convert.ToString(other, CultureInfo.InvariantCulture)
    };

    // ── auxiliares ────────────────────────────────────────────────────────────────────────────

    private static bool IsInt(decimal v) => v == Math.Truncate(v);

    private static T? Invalid<T>(IssueLog log, string collection, SourceDoc doc, string reason) where T : class
    {
        log.Add(collection, doc.Id, IssueKind.Invalid, reason);
        return null;
    }

    private static DateTime DateOrNow(Fields f, string name, DateTime now, IssueLog log, string collection, SourceDoc doc, out bool ok)
    {
        var d = f.Date(name, out ok);
        if (!ok) { log.Add(collection, doc.Id, IssueKind.Invalid, $"'{name}' não é uma data"); return now; }
        if (d is null) log.Add(collection, doc.Id, IssueKind.Warning, $"'{name}' ausente: assumido o momento da migração");
        return d ?? now;
    }

    /// <summary>Ausente/vazio → padrão; valor exato conhecido → enum; qualquer outro → item inválido (registrado).</summary>
    private static bool Enum_<T>(Fields f, string name, T defaultValue, out T value, IssueLog log, string collection, SourceDoc doc)
        where T : struct, Enum
    {
        var raw = f.String(name);
        if (raw is null) { value = defaultValue; return true; }
        if (Enum.TryParse(raw, ignoreCase: false, out value) && Enum.IsDefined(value)) return true;

        log.Add(collection, doc.Id, IssueKind.Invalid, $"'{name}' desconhecido: \"{raw}\"");
        value = defaultValue;
        return false;
    }
}
