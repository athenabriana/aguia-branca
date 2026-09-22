namespace AguiaBranca.FirestoreMigrator.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_AcceptsAllOptions()
    {
        var o = CliOptions.Parse(["--firestore-project", "meu-proj", "--mongo", "mongodb://x", "--database", "db", "--temp-password", "senha-1234", "--report", "r.json", "--timezone", "UTC"]);
        (o.FirestoreProject, o.MongoConnection, o.Database, o.TempPassword, o.ReportPath, o.TimeZone, o.DryRun).Should().Be(("meu-proj", "mongodb://x", "db", "senha-1234", "r.json", "UTC", false));
    }

    [Fact]
    public void DryRun_DoesNotNeedAPassword() =>
        CliOptions.Parse(["--firestore-project", "p", "--mongo", "m", "--dry-run"]).DryRun.Should().BeTrue();

    [Theory]
    [InlineData("--mongo", "m")]
    [InlineData("--firestore-project", "p")]
    public void MissingRequiredOptions_Throw(string opt, string value) =>
        new Action(() => CliOptions.Parse([opt, value, "--dry-run"])).Should().Throw<ArgumentException>();

    [Fact]
    public void WithoutDryRun_ATempPasswordIsRequired() =>
        new Action(() => CliOptions.Parse(["--firestore-project", "p", "--mongo", "m", "--temp-password", "curta"])).Should().Throw<ArgumentException>().WithMessage("*temp-password*");

    [Theory]
    [InlineData("--nao-existe")]
    [InlineData("--mongo")]
    public void UnknownOrValuelessOptions_Throw(string arg) =>
        new Action(() => CliOptions.Parse(["--firestore-project", "p", arg])).Should().Throw<ArgumentException>();

    [Fact]
    public void Help_ShortCircuitsValidation() => CliOptions.Parse(["--help"]).ShowHelp.Should().BeTrue();
}
