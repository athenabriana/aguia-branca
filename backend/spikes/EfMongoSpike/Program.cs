using EfMongoSpike;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

var connection = args.FirstOrDefault() ?? "mongodb://localhost:27017/?directConnection=true";
var dbName = "spike_" + Guid.NewGuid().ToString("N")[..8];
var client = new MongoClient(connection);
var raw = client.GetDatabase(dbName);

SpikeContext NewCtx() =>
    new(new DbContextOptionsBuilder<SpikeContext>().UseMongoDB(client, dbName).Options);
CamelContext NewCamel() =>
    new(new DbContextOptionsBuilder<CamelContext>().UseMongoDB(client, dbName + "_camel").Options);

var results = new List<(string id, string name, bool ok, string detail)>();
async Task Check(string id, string name, Func<Task<string>> body)
{
    try { var d = await body(); results.Add((id, name, true, d)); }
    catch (Exception ex) { results.Add((id, name, false, $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}")); }
}
static void Assert(bool cond, string msg) { if (!cond) throw new Exception("assert: " + msg); }

// ---------- (a) CRUD, ObjectId->string, enum string, owned docs
await Check("a1", "Insert + reload (string ObjectId, enum string, Ice embutido, lista embutida)", async () =>
{
    string id;
    await using (var c = NewCtx())
    {
        var idea = new Idea { Title = "Roteirização", Status = Status.Aprovada,
            Ice = new Ice { Impact = 8, Confidence = 7, Ease = 6 },
            Changes = [new Change { Field = "investment", From = "100", To = "120" }, new Change { Field = "stage", From = "A", To = "B" }] };
        id = idea.Id; c.Ideas.Add(idea); await c.SaveChangesAsync();
    }
    await using var c2 = NewCtx();
    var got = await c2.Ideas.FirstAsync(x => x.Id == id);
    Assert(got.Ice is { Impact: 8 } && got.Ice.Score == 336, "ice");
    Assert(got.Changes.Count == 2 && got.Status == Status.Aprovada && got.GuidelineId == null, "fields");
    Assert(got.CreatedAt.Kind == DateTimeKind.Utc, $"CreatedAt kind = {got.CreatedAt.Kind}");
    var doc = await raw.GetCollection<BsonDocument>("ideas").Find(new BsonDocument("_id", new ObjectId(id))).FirstAsync();
    Assert(doc["_id"].IsObjectId, "_id nativo ObjectId");
    Assert(doc["Status"].IsString && doc["Status"].AsString == "Aprovada", $"status string: {doc["Status"]}");
    return $"_id ObjectId nativo · campos no doc: {string.Join(",", doc.Names)}";
});
await Check("a2", "Update + Delete", async () =>
{
    await using var c = NewCtx();
    var i = new Idea { Title = "u" }; c.Ideas.Add(i); await c.SaveChangesAsync();
    i.Title = "u2"; i.Version++; await c.SaveChangesAsync();
    c.Ideas.Remove(i); await c.SaveChangesAsync();
    Assert(await c.Ideas.CountAsync(x => x.Id == i.Id) == 0, "deleted");
    return "ok";
});

// ---------- (b) transações
await Check("b1", "Transação multi-documento COMMIT (2 SaveChanges + 2 coleções)", async () =>
{
    await using var c = NewCtx();
    var u = new SpikeUser { UserName = "tx@x", Points = 0 }; c.Users.Add(u); await c.SaveChangesAsync();
    await using var tx = await c.Database.BeginTransactionAsync();
    var i = new Idea { Title = "tx-commit" }; c.Ideas.Add(i); await c.SaveChangesAsync();
    u.Points += 10; await c.SaveChangesAsync();
    await tx.CommitAsync();
    await using var v = NewCtx();
    Assert(await v.Ideas.AnyAsync(x => x.Id == i.Id), "idea persisted");
    Assert((await v.Users.FirstAsync(x => x.Id == u.Id)).Points == 10, "points persisted");
    return "commit ok";
});
await Check("b2", "Transação ROLLBACK explícito (nada persiste)", async () =>
{
    await using var c = NewCtx();
    var u = new SpikeUser { UserName = "rb@x", Points = 5 }; c.Users.Add(u); await c.SaveChangesAsync();
    string ideaId;
    await using (var tx = await c.Database.BeginTransactionAsync())
    {
        var i = new Idea { Title = "tx-rollback" }; ideaId = i.Id; c.Ideas.Add(i); await c.SaveChangesAsync();
        u.Points += 999; await c.SaveChangesAsync();
        await tx.RollbackAsync();
    }
    var n = await raw.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(new BsonDocument("_id", new ObjectId(ideaId)));
    var pts = (await raw.GetCollection<BsonDocument>("users").Find(new BsonDocument("_id", new ObjectId(u.Id))).FirstAsync())["Points"].ToInt32();
    Assert(n == 0, "idea não deve existir após rollback"); Assert(pts == 5, $"points deve continuar 5, veio {pts}");
    return "rollback ok (idea ausente, points intacto)";
});
await Check("b3", "Rollback por exceção (padrão do MongoUnitOfWork)", async () =>
{
    await using var c = NewCtx();
    string ideaId = "";
    try
    {
        await using var tx = await c.Database.BeginTransactionAsync();
        var i = new Idea { Title = "boom" }; ideaId = i.Id; c.Ideas.Add(i); await c.SaveChangesAsync();
        throw new InvalidOperationException("falha simulada");
    }
    catch (InvalidOperationException) { }
    var n = await raw.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(new BsonDocument("_id", new ObjectId(ideaId)));
    Assert(n == 0, "dispose sem commit deve reverter");
    return "dispose sem commit reverte";
});
await Check("b4", "SaveChanges único com 2 entidades: atomicidade quando a 2ª viola índice único", async () =>
{
    await raw.GetCollection<BsonDocument>("ideas").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Title"),
            new CreateIndexOptions { Unique = true, Name = "ux_title" }));
    await using var c = NewCtx();
    var a = new Idea { Title = "uniq-1" }; var b = new Idea { Title = "uniq-1" }; var ok = new Idea { Title = "uniq-0" };
    c.Ideas.AddRange(ok, a, b);
    string kind = "";
    try { await c.SaveChangesAsync(); throw new Exception("esperava falha"); }
    catch (MongoBulkWriteException ex) { kind = "MongoBulkWriteException/" + ex.WriteErrors[0].Category; }
    catch (DbUpdateException ex) { kind = "DbUpdateException"; _ = ex; }
    var n = await raw.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(new BsonDocument("Title", new BsonDocument("$in", new BsonArray { "uniq-0", "uniq-1" })));
    return $"exceção={kind}; após falha persistiram {n} de 3 docs ({(n == 0 ? "atômico ✔" : "NÃO atômico — exige transação explícita")})";
});

// ---------- (c) concorrência otimista
await Check("c1", "Concurrency token (Version): 2º writer com versão velha falha", async () =>
{
    string id;
    await using (var seed = NewCtx()) { var i = new Idea { Title = "conc", Version = 0 }; id = i.Id; seed.Ideas.Add(i); await seed.SaveChangesAsync(); }
    await using var c1 = NewCtx(); await using var c2 = NewCtx();
    var a = await c1.Ideas.FirstAsync(x => x.Id == id); var b = await c2.Ideas.FirstAsync(x => x.Id == id);
    a.Title = "A"; a.Version = 1; await c1.SaveChangesAsync();
    b.Title = "B"; b.Version = 1;
    try { await c2.SaveChangesAsync(); throw new Exception("2º writer NÃO falhou (sem detecção)"); }
    catch (DbUpdateConcurrencyException) { return "DbUpdateConcurrencyException lançada ✔"; }
});

// ---------- (d) consultas
await Check("d1", "Where + OrderByDescending + Skip/Take + Count + StartsWith", async () =>
{
    await using var c = NewCtx();
    var baseTime = DateTime.UtcNow.AddDays(-100);
    for (var k = 0; k < 25; k++)
        c.Ideas.Add(new Idea { Title = (k % 2 == 0 ? "page-A" : "page-B") + k, Status = k % 3 == 0 ? Status.Aprovada : Status.Submetida, CreatedAt = baseTime.AddDays(k) });
    await c.SaveChangesAsync();
    var q = c.Ideas.Where(x => x.Status == Status.Submetida && x.Title.StartsWith("page-"));
    var total = await q.CountAsync();
    var page = await q.OrderByDescending(x => x.CreatedAt).Skip(3).Take(5).ToListAsync();
    Assert(total == 16, $"total {total}"); Assert(page.Count == 5 && page[0].CreatedAt > page[4].CreatedAt, "ordem/página");
    return $"total={total}, página ok";
});
await Check("d2", "Select projection (anônimo) — limitação conhecida", async () =>
{
    await using var c = NewCtx();
    var r = await c.Ideas.Where(x => x.Title.StartsWith("page-A")).Select(x => new { x.Id, x.Title }).Take(2).ToListAsync();
    return $"suportado ({r.Count} itens)";
});
await Check("d3", "GroupBy no servidor — limitação conhecida", async () =>
{
    await using var c = NewCtx();
    var r = await c.Ideas.GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
    return $"suportado ({r.Count} grupos)";
});
await Check("d4", "Agregação em memória (fallback do design): materializa e agrupa no cliente", async () =>
{
    await using var c = NewCtx();
    var all = await c.Ideas.Where(x => x.Title.StartsWith("page-")).ToListAsync();
    var g = all.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count());
    return string.Join(", ", g.Select(kv => $"{kv.Key}={kv.Value}"));
});

// ---------- (e) Identity
await Check("e1", "Identity: UserManager + store customizado (create, hash, check, find, lockout)", async () =>
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDbContext<SpikeContext>(o => o.UseMongoDB(client, dbName));
    services.AddIdentityCore<SpikeUser>(o =>
    {
        o.Password.RequiredLength = 8; o.Password.RequireNonAlphanumeric = false; o.Password.RequireUppercase = false;
        o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    }).AddUserStore<SpikeUserStore>();
    await using var sp = services.BuildServiceProvider();
    using var scope = sp.CreateScope();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<SpikeUser>>();

    var user = new SpikeUser { UserName = "lider@aguiabranca.com", Email = "lider@aguiabranca.com", Role = "LIDER" };
    var created = await um.CreateAsync(user, "aguiabranca123");
    Assert(created.Succeeded, "create: " + string.Join(";", created.Errors.Select(e => e.Code)));
    Assert(user.PasswordHash is { Length: > 20 } && user.PasswordHash != "aguiabranca123", "hash");

    var weak = await um.CreateAsync(new SpikeUser { UserName = "w@x.com", Email = "w@x.com" }, "curta");
    Assert(!weak.Succeeded && weak.Errors.Any(e => e.Code == "PasswordTooShort"), "senha curta rejeitada");

    var dup = await um.CreateAsync(new SpikeUser { UserName = "dup@x.com", Email = "LIDER@aguiabranca.com" }, "aguiabranca123");
    Assert(!dup.Succeeded, "e-mail duplicado deveria ser rejeitado (RequireUniqueEmail)");

    var found = await um.FindByEmailAsync("LIDER@AGUIABRANCA.COM");
    Assert(found?.Id == user.Id, "find by email (case-insens)");
    Assert(await um.CheckPasswordAsync(found!, "aguiabranca123"), "senha certa");
    Assert(!await um.CheckPasswordAsync(found!, "errada123"), "senha errada");

    for (var i = 0; i < 5; i++) await um.AccessFailedAsync(found!);
    Assert(await um.IsLockedOutAsync(found!), "lockout após 5 falhas");
    var reloaded = await um.FindByIdAsync(found!.Id);
    Assert(reloaded!.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14), "lockout persistido");
    return "create/hash/senha fraca/e-mail único/find/check/lockout ✔";
});

// ---------- (f) índices via driver
await Check("f1", "Índices: único, único parcial (só docs com valor), TTL — via driver", async () =>
{
    var users = raw.GetCollection<BsonDocument>("users");
    await users.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys.Ascending("NormalizedEmail"),
        new CreateIndexOptions<BsonDocument> { Unique = true, Name = "ux_email", PartialFilterExpression = new BsonDocument("NormalizedEmail", new BsonDocument("$type", "string")) }));
    var ideas = raw.GetCollection<BsonDocument>("ideas");
    await ideas.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys.Ascending("GuidelineId"),
        new CreateIndexOptions<BsonDocument> { Unique = true, Name = "ux_guideline_partial", PartialFilterExpression = new BsonDocument("GuidelineId", new BsonDocument("$type", "string")) }));
    var ttl = raw.GetCollection<BsonDocument>("refreshTokens");
    await ttl.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys.Ascending("ExpiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_expires" }));

    await using var c = NewCtx();
    c.Ideas.AddRange(new Idea { Title = "p1" }, new Idea { Title = "p2" }); // dois com GuidelineId null: permitido pelo índice parcial
    await c.SaveChangesAsync();
    c.Ideas.AddRange(new Idea { Title = "p3", GuidelineId = "G1" }); await c.SaveChangesAsync();
    c.Ideas.Add(new Idea { Title = "p4", GuidelineId = "G1" });
    try { await c.SaveChangesAsync(); throw new Exception("duplicado deveria falhar"); }
    catch (MongoBulkWriteException ex) { Assert(ex.WriteErrors[0].Category == ServerErrorCategory.DuplicateKey, "categoria DuplicateKey"); }
    var names = (await (await ideas.Indexes.ListAsync()).ToListAsync()).Select(d => d["name"].AsString);
    return "criados; violação de único vira MongoBulkWriteException(DuplicateKey); índices: " + string.Join(",", names);
});

// ---------- (g) camelCase
await Check("g1", "Nomes de campo em camelCase via SetElementName no OnModelCreating (incl. tipos owned)", async () =>
{
    string id;
    await using (var c = NewCamel())
    {
        var i = new Idea { Title = "camel", Status = Status.Aprovada, GuidelineId = "G", Ice = new Ice { Impact = 1, Confidence = 2, Ease = 3 }, Changes = [new Change { Field = "f", From = "1", To = "2" }] };
        id = i.Id; c.Ideas.Add(i); await c.SaveChangesAsync();
    }
    var doc = await client.GetDatabase(dbName + "_camel").GetCollection<BsonDocument>("ideas").Find(new BsonDocument("_id", new ObjectId(id))).FirstAsync();
    var names = string.Join(",", doc.Names);
    Assert(doc.Contains("createdAt") && doc.Contains("guidelineId") && !doc.Contains("CreatedAt"), "top-level camel: " + names);
    Assert(doc["ice"].AsBsonDocument.Contains("impact"), "owned one camel: " + doc["ice"]);
    Assert(doc["changes"].AsBsonArray[0].AsBsonDocument.Contains("field"), "owned many camel: " + doc["changes"]);
    await using var c2 = NewCamel();
    var back = await c2.Ideas.FirstAsync(x => x.Id == id);
    Assert(back.Ice?.Impact == 1 && back.Changes.Count == 1 && back.GuidelineId == "G", "leitura de volta");
    await c2.Ideas.Where(x => x.GuidelineId == "G").CountAsync();
    return "campos: " + names;
});

await Check("g2", "Entidade SEM atributos do driver: Id string<->ObjectId (e referência opcional) via fluent API", async () =>
{
    var opts = new DbContextOptionsBuilder<PlainContext>().UseMongoDB(client, dbName + "_plain").Options;
    var gid = ObjectId.GenerateNewId().ToString();
    string id;
    await using (var c = new PlainContext(opts))
    {
        var i = new PlainIdea { Title = "plain", Status = Status.Aprovada, GuidelineId = gid };
        var j = new PlainIdea { Title = "plain-null" };
        id = i.Id; c.Ideas.AddRange(i, j); await c.SaveChangesAsync();
    }
    var doc = await client.GetDatabase(dbName + "_plain").GetCollection<BsonDocument>("plain_ideas").Find(new BsonDocument("_id", new ObjectId(id))).FirstAsync();
    Assert(doc["_id"].IsObjectId, "_id ObjectId");
    Assert(doc["GuidelineId"].IsObjectId, "guidelineId ObjectId nativo: " + doc["GuidelineId"].BsonType);
    await using var c2 = new PlainContext(opts);
    var back = await c2.Ideas.FirstAsync(x => x.Id == id);
    Assert(back.GuidelineId == gid && back.Status == Status.Aprovada, "leitura de volta");
    var byGuideline = await c2.Ideas.CountAsync(x => x.GuidelineId == gid);
    var nulls = await c2.Ideas.CountAsync(x => x.GuidelineId == null);
    Assert(byGuideline == 1 && nulls == 1, $"filtros por ref: {byGuideline}/{nulls}");
    return "Id e GuidelineId como ObjectId nativo; filtro por Id/GuidelineId/null funciona";
});

// ---------- relatório
Console.WriteLine();
foreach (var (id, name, ok, detail) in results)
    Console.WriteLine($"{(ok ? "✅" : "❌")} [{id}] {name}\n      {detail}");
await client.DropDatabaseAsync(dbName); await client.DropDatabaseAsync(dbName + "_camel"); await client.DropDatabaseAsync(dbName + "_plain");
Console.WriteLine($"\n{results.Count(r => r.ok)}/{results.Count} ok · banco {dbName} removido");
