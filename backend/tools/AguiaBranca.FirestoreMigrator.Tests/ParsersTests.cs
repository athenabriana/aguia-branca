using AguiaBranca.Domain.Enums;
using AguiaBranca.FirestoreMigrator.Migration;
using AguiaBranca.FirestoreMigrator.Source;
using AguiaBranca.FirestoreMigrator.Transform;

namespace AguiaBranca.FirestoreMigrator.Tests;

public class ParsersTests
{
    private static SourceDoc Doc(string id, Dictionary<string, object?> f) => new(id, f);

    [Fact]
    public void User_TimestampBecomesUtc_AndUnknownEnumIsInvalidAndReported()
    {
        var log = new IssueLog();
        var ok = Parsers.User(Doc("a", Sample.User("Ana", "ana@x.com", createdAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.FromHours(-3)).UtcDateTime)), Sample.Now, log)!;
        var bad = Parsers.User(Doc("b", Sample.User("Bia", "bia@x.com", role: "SUPERUSER")), Sample.Now, log);

        ok.CreatedAt.Should().Be(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        ok.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        bad.Should().BeNull();
        log.Items.Should().ContainSingle().Which.Should().Match<Issue>(i => i.Kind == IssueKind.Invalid && i.LegacyId == "b" && i.Message.Contains("SUPERUSER"));
    }

    [Fact]
    public void MissingFields_FallBackToTheAppDefaults_AndMissingDatesToNowWithAWarning()
    {
        var log = new IssueLog();
        var idea = Parsers.Idea(Doc("i", new() { ["title"] = "Só título", ["authorId"] = "u" }), Sample.Now, log)!;

        (idea.Division, idea.Status, idea.Category, idea.Description).Should().Be((Division.CORPORATIVO, IdeaStatus.SUBMETIDA, "Geral", string.Empty));
        idea.CreatedAt.Should().Be(Sample.Now);
        log.Items.Should().ContainSingle(i => i.Kind == IssueKind.Warning && i.Message.Contains("createdAt"));
    }

    [Theory]
    [InlineData(1000L, 1000.0)]
    [InlineData(1234.56, 1234.56)]
    [InlineData("777.5", 777.5)]
    public void Project_NumbersAcceptLongDoubleAndNumericStrings(object value, double expected)
    {
        var p = Parsers.Project(Doc("p", Sample.Project("P", "u", investment: value)), Sample.Now, new IssueLog())!;
        p.Investment.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("investment", "muito")]
    [InlineData("investment", -5L)]
    [InlineData("financialReturn", true)]
    public void Project_NonNumericOrNegativeMoney_IsInvalid(string field, object value)
    {
        var f = Sample.Project("P", "u");
        f[field] = value;
        var log = new IssueLog();

        Parsers.Project(Doc("p", f), Sample.Now, log).Should().BeNull();
        log.Items.Should().ContainSingle(i => i.Kind == IssueKind.Invalid && i.Message.Contains(field));
    }

    [Theory]
    [InlineData(0L, 5L, 5L)]
    [InlineData(11L, 5L, 5L)]
    [InlineData(5L, 5L, "x")]
    [InlineData(5.5, 5L, 5L)]
    public void Idea_IceOutOfRangeOrNotAnInteger_IsDroppedWithAWarning_ButTheIdeaMigrates(object impact, object confidence, object ease)
    {
        var log = new IssueLog();
        var idea = Parsers.Idea(Doc("i", Sample.Idea("Ideia", "u", "EM_ANALISE", ice: new Dictionary<string, object?> { ["impact"] = impact, ["confidence"] = confidence, ["ease"] = ease })), Sample.Now, log)!;

        idea.Ice.Should().BeNull();
        log.Items.Should().ContainSingle(i => i.Kind == IssueKind.Warning && i.Message.Contains("ICE"));
    }

    [Fact]
    public void ValidIce_IsKept_EvenWhenStoredAsDoubles()
    {
        var idea = Parsers.Idea(Doc("i", Sample.Idea("Ideia", "u", "EM_ANALISE", ice: new Dictionary<string, object?> { ["impact"] = 9.0, ["confidence"] = 8L, ["ease"] = 7L })), Sample.Now, new IssueLog())!;
        idea.Ice.Should().Be(new IceRec(9, 8, 7));
    }

    [Fact]
    public void Change_UsesTheServersCanonicalKindsAndFormats()
    {
        Parsers.Change("investment", 0L, 45000.0).Should().Be(new ChangeRec("investment", FieldValueKind.NUMBER, "0", "45000"));
        Parsers.Change("targetDate", null, new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new ChangeRec("targetDate", FieldValueKind.DATE, null, "2026-12-20T12:00:00Z"));
        Parsers.Change("stage", "PLANEJAMENTO", "EM_EXECUCAO").Should().Be(new ChangeRec("stage", FieldValueKind.TEXT, "PLANEJAMENTO", "EM_EXECUCAO"));
    }

    [Fact]
    public void Update_KeepsValidChanges_AndSkipsMalformedOnes()
    {
        var doc = Sample.Update("u", changes:
        [
            new Dictionary<string, object?> { ["field"] = "stage", ["from"] = "A", ["to"] = "B" },
            new Dictionary<string, object?> { ["from"] = 1L },
            "lixo"
        ]);

        var update = Parsers.Update("p1", Doc("x", doc), Sample.Now, new IssueLog())!;

        update.Changes.Should().ContainSingle().Which.Field.Should().Be("stage");
    }

    [Fact]
    public void RequiredFields_MissingTitleOrAuthor_MakeTheItemInvalid()
    {
        var log = new IssueLog();
        Parsers.Guideline(Doc("g", new() { ["authorId"] = "u" }), Sample.Now, log).Should().BeNull();
        Parsers.Idea(Doc("i", new() { ["title"] = "T" }), Sample.Now, log).Should().BeNull();
        Parsers.Project(Doc("p", new() { ["title"] = "T" }), Sample.Now, log).Should().BeNull();
        Parsers.User(Doc("u", new() { ["name"] = "Sem e-mail" }), Sample.Now, log).Should().BeNull();

        log.Items.Select(i => i.Collection).Should().BeEquivalentTo("guidelines", "ideas", "projects", "users");
        log.Items.Should().OnlyContain(i => i.Kind == IssueKind.Invalid);
    }
}
