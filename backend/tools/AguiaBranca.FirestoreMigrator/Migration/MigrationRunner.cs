using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;
using AguiaBranca.FirestoreMigrator.Source;
using AguiaBranca.FirestoreMigrator.Transform;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;

namespace AguiaBranca.FirestoreMigrator.Migration;

public sealed record MigrationOptions(bool DryRun, string? TempPassword, DateTime Now, TimeZoneInfo TimeZone);

/// <summary>
/// Firestore → MongoDB. Ordem: users → guidelines (+histórico CREATED) → ideas → projects → projectUpdates → pointEvents
/// (MIGRATION); badges recalculadas com o mesmo <c>BadgeEvaluator</c> do servidor. Idempotente: os <c>_id</c> vêm de um mapa
/// legado→ObjectId persistido (<c>migration_idmap</c>) e tudo é upsert por <c>_id</c>. Não escreve nada em <c>--dry-run</c>.
/// </summary>
public sealed class MigrationRunner(IFirestoreSource source, IMigrationSink sink, MigrationOptions options)
{
    private readonly IssueLog _log = new();
    private readonly Dictionary<string, ObjectId> _map = [];
    private readonly HashSet<string> _newKeys = [];

    public async Task<MigrationReport> RunAsync(CancellationToken ct)
    {
        if (!options.DryRun && (options.TempPassword is null || options.TempPassword.Length < 8))
            throw new InvalidOperationException("Informe --temp-password com no mínimo 8 caracteres (senha inicial dos usuários migrados).");

        // 1) leitura + validação/tipagem (puro)
        var userDocs = await source.ReadCollectionAsync(FirestorePaths.Users, ct);
        var guidelineDocs = await source.ReadCollectionAsync(FirestorePaths.Guidelines, ct);
        var ideaDocs = await source.ReadCollectionAsync(FirestorePaths.Ideas, ct);
        var projectDocs = await source.ReadCollectionAsync(FirestorePaths.Projects, ct);
        var updateDocs = new List<(string ProjectId, SourceDoc Doc)>();
        foreach (var p in projectDocs)
            foreach (var u in await source.ReadCollectionAsync(FirestorePaths.ProjectUpdates(p.Id), ct)) updateDocs.Add((p.Id, u));

        var users = Distinct(userDocs.Select(d => Parsers.User(d, options.Now, _log)).OfType<UserRec>(), u => u.Email.ToUpperInvariant(),
            Parsers.UsersCollection, u => u.LegacyId, "e-mail duplicado na origem");
        var guidelines = guidelineDocs.Select(d => Parsers.Guideline(d, options.Now, _log)).OfType<GuidelineRec>().ToList();
        var ideas = ideaDocs.Select(d => Parsers.Idea(d, options.Now, _log)).OfType<IdeaRec>().ToList();
        var projects = projectDocs.Select(d => Parsers.Project(d, options.Now, _log)).OfType<ProjectRec>().ToList();
        var updates = updateDocs.Select(x => Parsers.Update(x.ProjectId, x.Doc, options.Now, _log)).OfType<UpdateRec>().ToList();

        // 2) IDs (mapa persistido) + resolução de referências
        foreach (var (k, v) in await sink.LoadIdMapAsync(ct)) _map[k] = v;
        var existingUserIds = new Dictionary<string, ObjectId>();
        users = await AssignUsersAsync(users, existingUserIds, ct);

        var userIds = users.ToDictionary(u => u.LegacyId, u => Id(Parsers.UsersCollection, u.LegacyId));
        var guidelineList = ResolveRequiredAuthor(guidelines, g => g.AuthorId, g => g.LegacyId, Parsers.GuidelinesCollection, userIds);
        var guidelineIds = guidelineList.ToDictionary(g => g.LegacyId, g => Id(Parsers.GuidelinesCollection, g.LegacyId));
        var ideaList = ResolveRequiredAuthor(ideas, i => i.AuthorId, i => i.LegacyId, Parsers.IdeasCollection, userIds);
        var ideaIds = ideaList.ToDictionary(i => i.LegacyId, i => Id(Parsers.IdeasCollection, i.LegacyId));
        var projectList = ResolveRequiredAuthor(projects, p => p.CreatorManagerId, p => p.LegacyId, Parsers.ProjectsCollection, userIds);
        var projectIds = projectList.ToDictionary(p => p.LegacyId, p => Id(Parsers.ProjectsCollection, p.LegacyId));
        var updateList = ResolveUpdates(updates, projectIds, userIds);

        // 3) documentos
        var docs = new Docs();
        var hasher = new PasswordHasher<AppUser>();
        var throwaway = AppUser.Create("migracao", "migracao@invalid", Role.OPERADOR, Division.CORPORATIVO, options.Now);

        var badgesByUser = ComputeBadges(users, ideaList, userIds, guidelineIds);
        foreach (var u in users)
        {
            var id = userIds[u.LegacyId];
            var badges = badgesByUser[u.LegacyId];
            var isExisting = existingUserIds.ContainsKey(u.LegacyId);
            docs.Users.Add((id, isExisting ? UserProfileFields(u, badges) : NewUserDoc(u, badges, options.DryRun ? "-" : hasher.HashPassword(throwaway, options.TempPassword!)),
                isExisting ? UpsertMode.SetFields : UpsertMode.Replace));

            if (u.Points > 0)
                docs.PointEvents.Add((Id("pointEvents", u.LegacyId), new BsonDocument
                {
                    ["userId"] = id, ["delta"] = u.Points, ["reason"] = nameof(PointReason.MIGRATION), ["refId"] = BsonNull.Value,
                    ["createdAt"] = u.CreatedAt
                }));
        }

        foreach (var g in guidelineList)
        {
            var id = guidelineIds[g.LegacyId];
            docs.Guidelines.Add((id, GuidelineDoc(g, userIds[g.AuthorId])));
            docs.History.Add((Id("guidelineHistory", g.LegacyId), HistoryDoc(g, id, userIds[g.AuthorId])));
        }

        foreach (var i in ideaList)
            docs.Ideas.Add((ideaIds[i.LegacyId], IdeaDoc(i, userIds, guidelineIds)));

        var usedOrigins = new HashSet<string>();
        foreach (var p in projectList)
            docs.Projects.Add((projectIds[p.LegacyId], ProjectDoc(p, userIds, guidelineIds, ideaIds, usedOrigins)));

        foreach (var u in updateList)
            docs.Updates.Add((Id(Parsers.UpdatesCollection, $"{u.ProjectLegacyId}/{u.LegacyId}"), UpdateDoc(u, projectIds[u.ProjectLegacyId], userIds[u.AuthorId])));

        var violations = CountIntegrityViolations(docs);

        // 4) escrita (ordem do spec). O mapa de IDs é salvo ANTES: uma queda no meio não gera duplicatas na reexecução.
        if (!options.DryRun)
        {
            await sink.EnsureIndexesAsync(ct);
            await sink.SaveIdMapAsync(_map.Where(kv => _newKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value), ct);

            foreach (var (id, doc, mode) in docs.Users) await sink.UpsertAsync(Collections.Users, id, doc, mode, ct);
            foreach (var (id, doc) in docs.Guidelines) await sink.UpsertAsync(Collections.Guidelines, id, doc, UpsertMode.Replace, ct);
            foreach (var (id, doc) in docs.History) await sink.UpsertAsync(Collections.GuidelineHistory, id, doc, UpsertMode.Replace, ct);
            foreach (var (id, doc) in docs.Ideas) await sink.UpsertAsync(Collections.Ideas, id, doc, UpsertMode.Replace, ct);
            foreach (var (id, doc) in docs.Projects) await sink.UpsertAsync(Collections.Projects, id, doc, UpsertMode.Replace, ct);
            foreach (var (id, doc) in docs.Updates) await sink.UpsertAsync(Collections.ProjectUpdates, id, doc, UpsertMode.Replace, ct);
            foreach (var (id, doc) in docs.PointEvents) await sink.UpsertAsync(Collections.PointEvents, id, doc, UpsertMode.Replace, ct);
        }

        // 5) conciliação (no destino real, exceto em dry-run)
        var reports = new[]
        {
            await Reconcile(Parsers.UsersCollection, Collections.Users, userDocs.Count, docs.Users.Select(x => x.Id), ct),
            await Reconcile(Parsers.GuidelinesCollection, Collections.Guidelines, guidelineDocs.Count, docs.Guidelines.Select(x => x.Id), ct),
            await Reconcile(Parsers.IdeasCollection, Collections.Ideas, ideaDocs.Count, docs.Ideas.Select(x => x.Id), ct),
            await Reconcile(Parsers.ProjectsCollection, Collections.Projects, projectDocs.Count, docs.Projects.Select(x => x.Id), ct),
            await Reconcile(Parsers.UpdatesCollection, Collections.ProjectUpdates, updateDocs.Count, docs.Updates.Select(x => x.Id), ct)
        };

        return new MigrationReport(options.Now, options.DryRun, reports, _log.Items, violations, docs.History.Count + docs.PointEvents.Count);
    }

    // ── IDs ───────────────────────────────────────────────────────────────────────────────────

    private ObjectId Id(string collection, string legacyId)
    {
        var key = $"{collection}:{legacyId}";
        if (_map.TryGetValue(key, out var id)) return id;
        id = ObjectId.GenerateNewId();
        _map[key] = id;
        _newKeys.Add(key);
        return id;
    }

    /// <summary>Usuário com e-mail já usado no destino por OUTRA conta = conflito (descartado); a mesma conta = atualização.</summary>
    private async Task<List<UserRec>> AssignUsersAsync(List<UserRec> users, Dictionary<string, ObjectId> existing, CancellationToken ct)
    {
        var kept = new List<UserRec>();
        foreach (var u in users)
        {
            if (_map.TryGetValue($"users:{u.LegacyId}", out var mapped))
            {
                existing[u.LegacyId] = mapped;
                kept.Add(u);
                continue;
            }

            var found = await sink.FindUserByNormalizedEmailAsync(u.Email.ToUpperInvariant(), ct);
            if (found is null) { kept.Add(u); continue; }

            if (found.LegacyId == u.LegacyId)
            {
                _map[$"users:{u.LegacyId}"] = found.Id; // reexecução após queda: adota o documento já criado
                _newKeys.Add($"users:{u.LegacyId}");
                existing[u.LegacyId] = found.Id;
                kept.Add(u);
            }
            else
            {
                _log.Add(Parsers.UsersCollection, u.LegacyId, IssueKind.Conflict,
                    $"o e-mail {u.Email} já existe no destino (outro usuário): migre para um banco sem o seed (Seed__Enabled=false)");
            }
        }
        return kept;
    }

    // ── referências ───────────────────────────────────────────────────────────────────────────

    private List<T> Distinct<T>(IEnumerable<T> items, Func<T, string> key, string collection, Func<T, string> legacy, string reason)
    {
        var seen = new HashSet<string>();
        var kept = new List<T>();
        foreach (var item in items)
        {
            if (seen.Add(key(item))) kept.Add(item);
            else _log.Add(collection, legacy(item), IssueKind.Invalid, reason);
        }
        return kept;
    }

    private List<T> ResolveRequiredAuthor<T>(
        List<T> items, Func<T, string> authorOf, Func<T, string> legacyOf, string collection, Dictionary<string, ObjectId> userIds)
    {
        var kept = new List<T>();
        foreach (var item in items)
        {
            if (userIds.ContainsKey(authorOf(item))) kept.Add(item);
            else _log.Add(collection, legacyOf(item), IssueKind.OrphanReference, $"autor/gestor '{authorOf(item)}' não existe no destino: item descartado");
        }
        return kept;
    }

    private List<UpdateRec> ResolveUpdates(List<UpdateRec> updates, Dictionary<string, ObjectId> projectIds, Dictionary<string, ObjectId> userIds)
    {
        var kept = new List<UpdateRec>();
        foreach (var u in updates)
        {
            if (!projectIds.ContainsKey(u.ProjectLegacyId))
                _log.Add(Parsers.UpdatesCollection, $"{u.ProjectLegacyId}/{u.LegacyId}", IssueKind.OrphanReference, "projeto da atualização não foi migrado: descartada");
            else if (!userIds.ContainsKey(u.AuthorId))
                _log.Add(Parsers.UpdatesCollection, $"{u.ProjectLegacyId}/{u.LegacyId}", IssueKind.OrphanReference, $"autor '{u.AuthorId}' não existe no destino: descartada");
            else kept.Add(u);
        }
        return kept;
    }

    /// <summary>Referência opcional: existe → ObjectId; não existe → nulo + aviso de órfã.</summary>
    private BsonValue Optional(string? legacy, IReadOnlyDictionary<string, ObjectId> ids, string collection, string owner, string field)
    {
        if (legacy is null) return BsonNull.Value;
        if (ids.TryGetValue(legacy, out var id)) return id;
        _log.Add(collection, owner, IssueKind.OrphanReference, $"'{field}' aponta para '{legacy}', que não existe no destino: definido como nulo");
        return BsonNull.Value;
    }

    // ── documentos ────────────────────────────────────────────────────────────────────────────

    private BsonDocument NewUserDoc(UserRec u, IReadOnlyList<string> badges, string passwordHash) => new()
    {
        ["name"] = u.Name, ["email"] = u.Email, ["userName"] = u.Email,
        ["normalizedEmail"] = u.Email.ToUpperInvariant(), ["normalizedUserName"] = u.Email.ToUpperInvariant(),
        ["role"] = u.Role.ToString(), ["division"] = u.Division.ToString(), ["points"] = u.Points,
        ["badges"] = new BsonArray(badges), ["legacyId"] = u.LegacyId, ["createdAt"] = u.CreatedAt,
        ["passwordHash"] = passwordHash, ["securityStamp"] = Guid.NewGuid().ToString("N").ToUpperInvariant(),
        ["accessFailedCount"] = 0, ["lockoutEnd"] = BsonNull.Value, ["version"] = 1
    };

    /// <summary>Usuário já migrado: atualiza o perfil e o saldo; nunca mexe em credenciais.</summary>
    private static BsonDocument UserProfileFields(UserRec u, IReadOnlyList<string> badges) => new()
    {
        ["name"] = u.Name, ["role"] = u.Role.ToString(), ["division"] = u.Division.ToString(), ["points"] = u.Points,
        ["badges"] = new BsonArray(badges), ["legacyId"] = u.LegacyId, ["createdAt"] = u.CreatedAt
    };

    private static BsonDocument GuidelineDoc(GuidelineRec g, ObjectId author) => new()
    {
        ["title"] = g.Title, ["description"] = g.Description, ["pillar"] = g.Pillar.ToString(), ["campaign"] = BsonNull.Value,
        ["authorId"] = author, ["authorName"] = g.AuthorName, ["legacyId"] = g.LegacyId, ["createdAt"] = g.CreatedAt, ["updatedAt"] = g.UpdatedAt
    };

    private static BsonDocument HistoryDoc(GuidelineRec g, ObjectId guidelineId, ObjectId author) => new()
    {
        ["guidelineId"] = guidelineId, ["occurredAt"] = g.CreatedAt, ["category"] = g.Pillar.ToString(), ["campaign"] = BsonNull.Value,
        ["action"] = nameof(GuidelineAction.CREATED), ["title"] = g.Title,
        ["snapshot"] = new BsonDocument { ["title"] = g.Title, ["description"] = g.Description, ["pillar"] = g.Pillar.ToString(), ["campaign"] = BsonNull.Value },
        ["changedById"] = author, ["changedByName"] = g.AuthorName
    };

    private BsonDocument IdeaDoc(IdeaRec i, IReadOnlyDictionary<string, ObjectId> userIds, IReadOnlyDictionary<string, ObjectId> guidelineIds) => new()
    {
        ["title"] = i.Title, ["description"] = i.Description, ["category"] = i.Category, ["division"] = i.Division.ToString(),
        ["guidelineId"] = Optional(i.GuidelineId, guidelineIds, Parsers.IdeasCollection, i.LegacyId, "guidelineId"),
        ["authorId"] = userIds[i.AuthorId], ["authorName"] = i.AuthorName, ["status"] = i.Status.ToString(),
        ["ice"] = i.Ice is null ? BsonNull.Value
            : new BsonDocument { ["impact"] = i.Ice.Impact, ["confidence"] = i.Ice.Confidence, ["ease"] = i.Ice.Ease, ["score"] = i.Ice.Impact * i.Ice.Confidence * i.Ice.Ease },
        ["reviewerId"] = Optional(i.ReviewerId, userIds, Parsers.IdeasCollection, i.LegacyId, "reviewerId"),
        ["reviewComment"] = i.ReviewComment is null ? BsonNull.Value : i.ReviewComment,
        ["legacyId"] = i.LegacyId, ["createdAt"] = i.CreatedAt, ["updatedAt"] = i.UpdatedAt,
        ["reviewedAt"] = i.ReviewedAt is { } r ? r : BsonNull.Value
    };

    private BsonDocument ProjectDoc(
        ProjectRec p, IReadOnlyDictionary<string, ObjectId> userIds, IReadOnlyDictionary<string, ObjectId> guidelineIds,
        IReadOnlyDictionary<string, ObjectId> ideaIds, HashSet<string> usedOrigins)
    {
        // Índice único parcial em originatingIdeaId: só um projeto por ideia. O segundo perde o vínculo (registrado).
        var origin = Optional(p.OriginatingIdeaId, ideaIds, Parsers.ProjectsCollection, p.LegacyId, "originatingIdeaId");
        if (!origin.IsBsonNull && !usedOrigins.Add(p.OriginatingIdeaId!))
        {
            _log.Add(Parsers.ProjectsCollection, p.LegacyId, IssueKind.OrphanReference, $"a ideia '{p.OriginatingIdeaId}' já é origem de outro projeto: vínculo removido");
            origin = BsonNull.Value;
        }

        return new BsonDocument
        {
            ["title"] = p.Title, ["description"] = p.Description, ["stage"] = p.Stage.ToString(), ["statusText"] = p.StatusText,
            ["investment"] = (Decimal128)p.Investment, ["targetDate"] = p.TargetDate is { } t ? t : BsonNull.Value,
            ["financialReturn"] = (Decimal128)p.FinancialReturn, ["productivityGain"] = (Decimal128)p.ProductivityGain,
            ["costReduction"] = (Decimal128)p.CostReduction, ["division"] = p.Division.ToString(),
            ["guidelineId"] = Optional(p.GuidelineId, guidelineIds, Parsers.ProjectsCollection, p.LegacyId, "guidelineId"),
            ["creatorManagerId"] = userIds[p.CreatorManagerId], ["originatingIdeaId"] = origin,
            ["priorityScore"] = p.PriorityScore is { } ps ? ps : BsonNull.Value,
            ["reporterId"] = Optional(p.ReporterId, userIds, Parsers.ProjectsCollection, p.LegacyId, "reporterId"),
            ["reporterName"] = p.ReporterName is null ? BsonNull.Value : p.ReporterName,
            ["responsibleId"] = Optional(p.ResponsibleId, userIds, Parsers.ProjectsCollection, p.LegacyId, "responsibleId"),
            ["responsibleName"] = p.ResponsibleName is null ? BsonNull.Value : p.ResponsibleName,
            ["version"] = 1, ["legacyId"] = p.LegacyId, ["createdAt"] = p.CreatedAt, ["updatedAt"] = p.UpdatedAt
        };
    }

    private static BsonDocument UpdateDoc(UpdateRec u, ObjectId projectId, ObjectId author) => new()
    {
        ["projectId"] = projectId, ["authorId"] = author, ["authorName"] = u.AuthorName, ["note"] = u.Note,
        ["changes"] = new BsonArray(u.Changes.Select(c => new BsonDocument
        {
            ["field"] = c.Field, ["kind"] = c.Kind.ToString(), ["from"] = c.From is null ? BsonNull.Value : c.From, ["to"] = c.To is null ? BsonNull.Value : c.To
        })),
        ["createdAt"] = u.CreatedAt
    };

    // ── badges (mesmo avaliador do servidor) ──────────────────────────────────────────────────

    /// <summary>
    /// Recalcula as badges com o <see cref="BadgeEvaluator"/> do domínio. Ele trabalha com entidades, então cada ideia migrada
    /// vira uma ideia "de avaliação" em memória (só autor, orientação, status e data importam para as regras); nada disso é gravado.
    /// </summary>
    private Dictionary<string, IReadOnlyList<string>> ComputeBadges(
        List<UserRec> users, List<IdeaRec> ideas, Dictionary<string, ObjectId> userIds, Dictionary<string, ObjectId> guidelineIds)
    {
        const string reviewer = "665f00000000000000000001";
        var result = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var u in users)
        {
            var evaluator = AppUser.Create(u.Name, u.Email, u.Role, u.Division, options.Now);
            var authored = ideas.Where(i => i.AuthorId == u.LegacyId).Select(i =>
            {
                var g = i.GuidelineId is not null && guidelineIds.TryGetValue(i.GuidelineId, out var gid) ? gid.ToString() : null;
                var idea = Idea.Create("Ideia migrada", string.Empty, "Geral", i.Division, g, evaluator.Id, evaluator.Name, i.CreatedAt);
                switch (i.Status)
                {
                    case IdeaStatus.EM_ANALISE: idea.SaveIce(new Ice(1, 1, 1), reviewer, i.CreatedAt); break;
                    case IdeaStatus.APROVADA: idea.Approve(reviewer, i.CreatedAt); break;
                    case IdeaStatus.IMPLEMENTADA: idea.Approve(reviewer, i.CreatedAt); idea.MarkImplemented(i.CreatedAt); break;
                    case IdeaStatus.REJEITADA: idea.Reject(reviewer, "migrada", i.CreatedAt); break;
                }
                return idea;
            }).ToList();

            var earned = BadgeEvaluator.Evaluate(evaluator, authored, options.TimeZone);
            result[u.LegacyId] = Badges.All.Where(earned.Contains).ToList();
        }
        return result;
    }

    // ── integridade e conciliação ─────────────────────────────────────────────────────────────

    /// <summary>Confere, nos documentos finais, se todo ObjectId de referência aponta para um documento que será migrado.</summary>
    private static int CountIntegrityViolations(Docs d)
    {
        var known = new Dictionary<string, HashSet<ObjectId>>
        {
            ["users"] = d.Users.Select(x => x.Id).ToHashSet(), ["guidelines"] = d.Guidelines.Select(x => x.Id).ToHashSet(),
            ["ideas"] = d.Ideas.Select(x => x.Id).ToHashSet(), ["projects"] = d.Projects.Select(x => x.Id).ToHashSet()
        };
        var refs = new (string Field, string Target)[]
        {
            ("authorId", "users"), ("reviewerId", "users"), ("reporterId", "users"), ("responsibleId", "users"), ("creatorManagerId", "users"),
            ("changedById", "users"), ("userId", "users"), ("guidelineId", "guidelines"), ("originatingIdeaId", "ideas"), ("projectId", "projects")
        };

        var violations = 0;
        foreach (var doc in d.Guidelines.Select(x => x.Doc).Concat(d.History.Select(x => x.Doc)).Concat(d.Ideas.Select(x => x.Doc))
                     .Concat(d.Projects.Select(x => x.Doc)).Concat(d.Updates.Select(x => x.Doc)).Concat(d.PointEvents.Select(x => x.Doc)))
            foreach (var (field, target) in refs)
                if (doc.TryGetValue(field, out var v) && v.IsObjectId && !known[target].Contains(v.AsObjectId)) violations++;
        return violations;
    }

    private async Task<CollectionReport> Reconcile(string name, string mongoCollection, int sourceCount, IEnumerable<ObjectId> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        var verified = options.DryRun ? (long?)null : await sink.CountByIdsAsync(mongoCollection, idList, ct);
        return new CollectionReport(name, sourceCount,
            _log.Count(name, IssueKind.Invalid), _log.Count(name, IssueKind.Conflict) + DroppedByOrphan(name),
            idList.Count, verified,
            _log.Items.Count(i => i.Collection == name && i.Kind == IssueKind.OrphanReference), _log.Count(name, IssueKind.Warning));
    }

    /// <summary>Itens descartados por referência obrigatória órfã (contam como "não migrados", junto dos conflitos).</summary>
    private int DroppedByOrphan(string name) =>
        _log.Items.Count(i => i.Collection == name && i.Kind == IssueKind.OrphanReference && i.Message.Contains("descartad"));

    private sealed class Docs
    {
        public List<(ObjectId Id, BsonDocument Doc, UpsertMode Mode)> Users { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> Guidelines { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> History { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> Ideas { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> Projects { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> Updates { get; } = [];
        public List<(ObjectId Id, BsonDocument Doc)> PointEvents { get; } = [];
    }
}
