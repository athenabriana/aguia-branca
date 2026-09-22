using AguiaBranca.FirestoreMigrator;
using AguiaBranca.FirestoreMigrator.Migration;
using AguiaBranca.FirestoreMigrator.Source;

try
{
    var cli = CliOptions.Parse(args);
    if (cli.ShowHelp) { Console.WriteLine(CliOptions.Usage); return 0; }

    Console.WriteLine(cli.DryRun ? "Modo dry-run: nada será escrito no MongoDB." : $"Migrando para o banco '{cli.Database}'.");
    var source = await FirestoreSource.ConnectAsync(cli.FirestoreProject, cli.CredentialsPath);
    var sink = MongoSink.Connect(cli.MongoConnection, cli.Database);
    var runner = new MigrationRunner(source, sink, new MigrationOptions(cli.DryRun, cli.TempPassword, DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(cli.TimeZone)));

    var report = await runner.RunAsync(CancellationToken.None);

    Console.WriteLine(report.ToConsole());
    if (cli.ReportPath is not null)
    {
        await File.WriteAllTextAsync(cli.ReportPath, report.ToJson());
        Console.WriteLine($"Relatório JSON: {cli.ReportPath}");
    }
    return report.Reconciled ? 0 : 2;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}\n\n{CliOptions.Usage}");
    return 1;
}
catch (Exception ex) when (ex is InvalidOperationException or TimeZoneNotFoundException or IOException)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return 1;
}
