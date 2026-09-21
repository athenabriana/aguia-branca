using AguiaBranca.FirestoreMigrator.Migration;
using MongoDB.Bson;

namespace AguiaBranca.FirestoreMigrator.Tests;

public class MigrationRunnerTests
{
    private static Task<MigrationReport> Run(InMemorySource source, InMemorySink sink, bool dryRun = false) =>
        new MigrationRunner(source, sink, Sample.Options(dryRun)).RunAsync(default);

    private static BsonDocument ByLegacy(InMemorySink sink, string collection, string legacyId) =>
        sink.Docs(collection).Single(d => d.GetValue("legacyId", BsonNull.Value) == legacyId);

    [Fact]
    public async Task FullMigration_ReconcilesSourceAndDestination_WithNoIntegrityViolations()
    {
        var sink = new InMemorySink();
        var report = await Run(Sample.Full(), sink);

        report.Reconciled.Should().BeTrue(report.ToConsole());
        report.IntegrityViolations.Should().Be(0);
        report.Collections.Select(c => (c.Collection, c.Source, c.Migrated, c.VerifiedInDestination)).Should().Equal(
            ("users", 3, 3, 3L), ("guidelines", 2, 2, 2L), ("ideas", 3, 3, 3L), ("projects", 2, 2, 2L), ("projectUpdates", 2, 2, 2L));
        sink.Docs("guidelineHistory").Should().HaveCount(2);
        report.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task EveryReference_IsRemappedToTheNewObjectId_AndKeepsTheLegacyId()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        var op = ByLegacy(sink, "users", "u-op");
        var gestor = ByLegacy(sink, "users", "u-gestor");
        var g1 = ByLegacy(sink, "guidelines", "g1");
        var i1 = ByLegacy(sink, "ideas", "i1");
        var p1 = ByLegacy(sink, "projects", "p1");

        i1["authorId"].Should().Be(op["_id"]);
        i1["guidelineId"].Should().Be(g1["_id"]);
        p1["creatorManagerId"].Should().Be(gestor["_id"]);
        p1["responsibleId"].Should().Be(gestor["_id"]);
        p1["originatingIdeaId"].Should().Be(i1["_id"]);
        p1["guidelineId"].Should().Be(g1["_id"]);
        g1["authorId"].Should().Be(ByLegacy(sink, "users", "u-lider")["_id"]);
        sink.Docs("projectUpdates").Select(u => u["projectId"]).Should().BeEquivalentTo([p1["_id"], ByLegacy(sink, "projects", "p2")["_id"]]);
        sink.Docs("projectUpdates").Should().OnlyContain(u => u["authorId"] == gestor["_id"]);
        new[] { op, gestor, g1, i1, p1 }.Select(d => d["_id"].BsonType).Should().OnlyContain(t => t == BsonType.ObjectId);
    }

    [Fact]
    public async Task SecondRun_IsIdempotent_SameIdsSameDocumentsNothingDuplicated()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);
        var idsBefore = sink.Data.ToDictionary(c => c.Key, c => c.Value.Keys.OrderBy(k => k).ToList());
        var totalBefore = sink.Total;

        var second = await Run(Sample.Full(), sink);

        sink.Total.Should().Be(totalBefore);
        sink.Data.ToDictionary(c => c.Key, c => c.Value.Keys.OrderBy(k => k).ToList()).Should().BeEquivalentTo(idsBefore);
        second.Reconciled.Should().BeTrue();
        second.Collections.Should().OnlyContain(c => c.VerifiedInDestination == c.Migrated);
    }

    [Fact]
    public async Task DryRun_WritesNothing_NotEvenTheIdMap_ButStillReportsWhatWouldHappen()
    {
        var sink = new InMemorySink();

        var report = await Run(Sample.Full(), sink, dryRun: true);

        sink.Total.Should().Be(0);
        sink.IdMap.Should().BeEmpty();
        sink.Calls.Should().BeEmpty();
        report.DryRun.Should().BeTrue();
        report.Collections.Should().OnlyContain(c => c.VerifiedInDestination == null);
        report.Collections.Select(c => c.Migrated).Should().Equal(3, 2, 3, 2, 2);
        report.Reconciled.Should().BeTrue();
    }

    [Fact]
    public async Task IdMap_IsSavedBeforeAnyDocument_SoACrashMidWayCannotCreateDuplicates()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        sink.Calls.IndexOf("idmap").Should().BeLessThan(sink.Calls.IndexOf("users"));
        sink.Calls.IndexOf("indexes").Should().BeLessThan(sink.Calls.IndexOf("idmap"));
        sink.Calls.Where(c => c != "indexes" && c != "idmap").Distinct().Should()
            .Equal("users", "guidelines", "guidelineHistory", "ideas", "projects", "projectUpdates", "pointEvents");
        sink.IdMap.Keys.Should().Contain(["users:u-op", "guidelines:g1", "ideas:i1", "projects:p1", "projectUpdates:p1/up1"]);
    }

    [Fact]
    public async Task OrphanOptionalReferences_BecomeNull_AndAreListed_WhileRequiredOnesDiscardTheItem()
    {
        var s = Sample.Full();
        s.Add("ideas", "i-orfa", Sample.Idea("Ideia com orientação apagada", "u-op", guideline: "g-inexistente"));
        s.Add("ideas", "i-sem-autor", Sample.Idea("Ideia de usuário removido", "u-fantasma"));
        s.Add("projects", "p-orfao", Sample.Project("Projeto de gestor removido", "u-fantasma"));
        s.Add("projects/p-inexistente/updates", "up-x", Sample.Update("u-gestor"));
        var sink = new InMemorySink();

        var report = await Run(s, sink);

        ByLegacy(sink, "ideas", "i-orfa")["guidelineId"].IsBsonNull.Should().BeTrue();
        sink.Docs("ideas").Should().NotContain(d => d["legacyId"] == "i-sem-autor");
        sink.Docs("projects").Should().NotContain(d => d["legacyId"] == "p-orfao");
        report.Issues.Should().Contain(i => i.LegacyId == "i-orfa" && i.Kind == IssueKind.OrphanReference && i.Message.Contains("g-inexistente"));
        report.Issues.Should().Contain(i => i.LegacyId == "i-sem-autor" && i.Message.Contains("u-fantasma"));
        report.IntegrityViolations.Should().Be(0);
        report.Collections.Single(c => c.Collection == "ideas").Should().Match<CollectionReport>(c => c.Source == 5 && c.Migrated == 4 && c.Discarded == 1);
        report.Reconciled.Should().BeTrue("descartados e migrados fecham com a origem");
    }

    [Fact]
    public async Task InvalidItems_AreReportedAndTheMigrationContinues()
    {
        var s = Sample.Full();
        var bad = Sample.Idea("Status estranho", "u-op", "EM_REVISAO");
        s.Add("ideas", "i-ruim", bad);
        s.Add("projects", "p-ruim", Sample.Project("Estágio estranho", "u-gestor", "PAUSADO"));
        var sink = new InMemorySink();

        var report = await Run(s, sink);

        report.Issues.Should().Contain(i => i.LegacyId == "i-ruim" && i.Kind == IssueKind.Invalid);
        report.Issues.Should().Contain(i => i.LegacyId == "p-ruim" && i.Kind == IssueKind.Invalid);
        sink.Docs("ideas").Should().HaveCount(3);
        sink.Docs("projects").Should().HaveCount(2);
        report.Reconciled.Should().BeTrue();
        report.ToConsole().Should().Contain("Invalid").And.Contain("i-ruim");
    }

    [Fact]
    public async Task Points_BecomeAnOpeningMigrationEvent_DatedAtTheUsersCreation_AndUsersPointsMatch()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        var op = ByLegacy(sink, "users", "u-op");
        var events = sink.Docs("pointEvents").ToList();

        op["points"].AsInt32.Should().Be(265);
        var e = events.Should().ContainSingle("só quem tem saldo > 0 ganha evento").Subject;
        (e["userId"], e["delta"].AsInt32, e["reason"].AsString, e["refId"].IsBsonNull, e["createdAt"].ToUniversalTime()).Should()
            .Be((op["_id"], 265, "MIGRATION", true, Sample.T(1, 10)));
        events.Sum(x => x["delta"].AsInt32).Should().Be(sink.Docs("users").Sum(u => u["points"].AsInt32), "razão e saldo fecham");
    }

    [Fact]
    public async Task Badges_AreRecomputedByTheServersEvaluator_NotCopiedFromFirestore()
    {
        var s = Sample.Full(); // u-op: ideia IMPLEMENTADA vinculada a g1 + 2 outras
        var sink = new InMemorySink();
        await Run(s, sink);

        ByLegacy(sink, "users", "u-op")["badges"].AsBsonArray.Select(b => b.AsString)
            .Should().Equal("Primeira Ideia", "Estrategista", "Impacto Real");
        ByLegacy(sink, "users", "u-lider")["badges"].AsBsonArray.Should().BeEmpty();
    }

    [Fact]
    public async Task GuidelinesGetAnInitialCreatedHistoryEntry_WithSnapshotAndNoCampaign()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        var g1 = ByLegacy(sink, "guidelines", "g1");
        var h = sink.Docs("guidelineHistory").Single(x => x["guidelineId"] == g1["_id"]);

        (h["action"].AsString, h["category"].AsString, h["title"].AsString, h["changedById"], h["campaign"].IsBsonNull, h["occurredAt"].ToUniversalTime())
            .Should().Be(("CREATED", "IDEIAS", "Eficiência", ByLegacy(sink, "users", "u-lider")["_id"], true, Sample.T(2, 1)));
        h["snapshot"]["title"].AsString.Should().Be("Eficiência");
    }

    [Fact]
    public async Task Users_AreCreatedWithAHashedTempPassword_NeverThePlainText()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        var u = ByLegacy(sink, "users", "u-gestor");
        u["passwordHash"].AsString.Should().NotContain("senha-temporaria-1").And.NotBeNullOrEmpty();
        (u["email"].AsString, u["normalizedEmail"].AsString, u["role"].AsString, u["version"].AsInt32).Should().Be(("gestor@x.com", "GESTOR@X.COM", "GESTOR", 1));
        u["securityStamp"].AsString.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TempPassword_IsRequired_ExceptInDryRun()
    {
        var act = () => new MigrationRunner(Sample.Full(), new InMemorySink(), Sample.Options() with { TempPassword = "curta" }).RunAsync(default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*temp-password*");
    }

    [Fact]
    public async Task EmailAlreadyTakenByAnotherAccount_IsAConflict_AndItsDataBecomesOrphans()
    {
        var sink = new InMemorySink();
        var existingId = ObjectId.GenerateNewId();
        await sink.UpsertAsync("users", existingId, new BsonDocument { ["normalizedEmail"] = "OP@X.COM", ["legacyId"] = BsonNull.Value, ["name"] = "Do seed" }, UpsertMode.Replace, default);

        var report = await Run(Sample.Full(), sink);

        report.Issues.Should().Contain(i => i.Kind == IssueKind.Conflict && i.LegacyId == "u-op");
        sink.Docs("users").Should().HaveCount(3, "o usuário do seed + lider + gestor; o 'op' não é duplicado nem sobrescrito");
        sink.Docs("users").Single(d => d["_id"] == existingId)["name"].AsString.Should().Be("Do seed");
        sink.Docs("ideas").Should().BeEmpty("as ideias da operadora ficam órfãs de autor e são descartadas (reportado)");
        report.Reconciled.Should().BeTrue();
    }

    [Fact]
    public async Task ExistingMigratedUser_GetsProfileUpdated_ButCredentialsAreUntouched()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);
        var op = ByLegacy(sink, "users", "u-op");
        op["passwordHash"] = "hash-que-o-usuario-trocou";
        sink.Data["users"][op["_id"].AsObjectId] = op;

        var changed = Sample.Full();
        changed.Add("users", "u-novo", Sample.User("Novo", "novo@x.com"));
        await new MigrationRunner(new InMemorySource().Add("users", "u-op", Sample.User("Operadora Renomeada", "op@x.com", points: 300)), sink, Sample.Options()).RunAsync(default);

        var after = ByLegacy(sink, "users", "u-op");
        (after["name"].AsString, after["points"].AsInt32, after["passwordHash"].AsString).Should().Be(("Operadora Renomeada", 300, "hash-que-o-usuario-trocou"));
        sink.Docs("users").Should().HaveCount(3);
    }

    [Fact]
    public async Task TwoProjectsFromTheSameIdea_KeepOnlyTheFirstLink_BecauseOfTheUniqueIndex()
    {
        var s = Sample.Full();
        s.Add("projects", "p-dup", Sample.Project("Segundo projeto da mesma ideia", "u-gestor", idea: "i1"));
        var sink = new InMemorySink();

        var report = await Run(s, sink);

        var linked = sink.Docs("projects").Where(p => !p["originatingIdeaId"].IsBsonNull).ToList();
        linked.Select(p => p["originatingIdeaId"]).Distinct().Should().HaveCount(linked.Count);
        report.Issues.Should().Contain(i => i.LegacyId == "p-dup" && i.Message.Contains("já é origem"));
    }

    [Fact]
    public async Task DuplicateEmailsInTheSource_KeepTheFirst_AndReportTheOther()
    {
        var s = Sample.Full();
        s.Add("users", "u-clone", Sample.User("Clone", "OP@x.com"));
        var sink = new InMemorySink();

        var report = await Run(s, sink);

        sink.Docs("users").Should().HaveCount(3);
        report.Issues.Should().Contain(i => i.LegacyId == "u-clone" && i.Kind == IssueKind.Invalid && i.Message.Contains("duplicado"));
    }

    [Fact]
    public async Task ProjectMoney_IsWrittenAsDecimal128_AndDatesAsUtc()
    {
        var sink = new InMemorySink();
        await Run(Sample.Full(), sink);

        var p = ByLegacy(sink, "projects", "p1");
        p["investment"].BsonType.Should().Be(BsonType.Decimal128);
        p["investment"].AsDecimal.Should().Be(1000m);
        p["financialReturn"].AsDecimal.Should().Be(2500m);
        p["productivityGain"].AsDecimal.Should().Be(10.5m);
        p["targetDate"].ToUniversalTime().Should().Be(Sample.T(12, 1));
        (p["version"].AsInt32, p["priorityScore"].AsInt32).Should().Be((1, 648));
    }

    [Fact]
    public async Task ReportJson_ContainsTheReconciliation()
    {
        var report = await Run(Sample.Full(), new InMemorySink());
        var json = System.Text.Json.JsonDocument.Parse(report.ToJson()).RootElement;

        json.GetProperty("reconciled").GetBoolean().Should().BeTrue();
        json.GetProperty("collections").GetArrayLength().Should().Be(5);
        json.GetProperty("collections")[0].GetProperty("collection").GetString().Should().Be("users");
    }
}
