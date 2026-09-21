namespace AguiaBranca.FirestoreMigrator;

public sealed record CliOptions(
    string FirestoreProject, string? CredentialsPath, string MongoConnection, string Database, bool DryRun, string? TempPassword,
    string? ReportPath, string TimeZone, bool ShowHelp)
{
    public const string Usage = """
        Uso: aguiabranca-firestore-migrator [opções]

          --firestore-project <id>   Projeto do Firebase/Firestore de origem (obrigatório)
          --credentials <arquivo>    JSON da service account. Sem ele, usa o emulador (FIRESTORE_EMULATOR_HOST)
          --mongo <connection>       Connection string do MongoDB de destino (obrigatório)
          --database <nome>          Banco de destino (padrão: aguiabranca)
          --temp-password <senha>    Senha inicial dos usuários migrados (≥ 8 caracteres; ou env MIGRATION_TEMP_PASSWORD)
          --dry-run                  Lê, valida e concilia SEM escrever no MongoDB
          --report <arquivo.json>    Salva o relatório de conciliação em JSON
          --timezone <id>            Fuso do "mês corrente" das badges (padrão: America/Sao_Paulo)
          -h, --help                 Esta ajuda

        Códigos de saída: 0 = concluído e conciliado · 2 = concluído com divergências · 1 = erro de uso/execução.
        """;

    public static CliOptions Parse(string[] args)
    {
        string? project = null, credentials = null, mongo = null, password = Environment.GetEnvironmentVariable("MIGRATION_TEMP_PASSWORD"), report = null;
        var database = "aguiabranca";
        var timeZone = "America/Sao_Paulo";
        bool dryRun = false, help = false;

        string Value(ref int i)
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"A opção {args[i]} exige um valor.");
            return args[++i];
        }

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--firestore-project": project = Value(ref i); break;
                case "--credentials": credentials = Value(ref i); break;
                case "--mongo": mongo = Value(ref i); break;
                case "--database": database = Value(ref i); break;
                case "--temp-password": password = Value(ref i); break;
                case "--report": report = Value(ref i); break;
                case "--timezone": timeZone = Value(ref i); break;
                case "--dry-run": dryRun = true; break;
                case "-h" or "--help": help = true; break;
                default: throw new ArgumentException($"Opção desconhecida: {args[i]}");
            }
        }

        if (help) return new CliOptions("", null, "", database, false, null, null, timeZone, ShowHelp: true);

        if (string.IsNullOrWhiteSpace(project)) throw new ArgumentException("--firestore-project é obrigatório.");
        if (string.IsNullOrWhiteSpace(mongo)) throw new ArgumentException("--mongo é obrigatório.");
        if (credentials is not null && !File.Exists(credentials)) throw new ArgumentException($"Arquivo de credenciais não encontrado: {credentials}");
        if (!dryRun && (password is null || password.Length < 8)) throw new ArgumentException("--temp-password (≥ 8 caracteres) é obrigatório fora do --dry-run.");

        return new CliOptions(project, credentials, mongo, database, dryRun, password, report, timeZone, ShowHelp: false);
    }
}
