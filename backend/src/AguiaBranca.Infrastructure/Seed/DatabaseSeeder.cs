using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;
using AguiaBranca.Infrastructure.Configuration;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Seed;

/// <summary>
/// Dados de demonstração (R2-01.8). Idempotente e retomável: usuários são criados se não existirem; o restante entra numa
/// única transação apenas quando ainda não há orientações. Pontos, eventos e badges são coerentes com as regras (R-06).
/// </summary>
internal sealed class DatabaseSeeder(
    IServiceScopeFactory scopes,
    IOptions<SeedOptions> options,
    IOptions<ReportsOptions> reports,
    IHostEnvironment environment,
    ILogger<DatabaseSeeder> logger) : IHostedService
{
    public const string DemoPassword = "aguiabranca123";

    public static readonly (string Key, string Name, string Email, Role Role, Division Division)[] DemoUsers =
    [
        ("lider", "Líder INOVAGAB", "lider@aguiabranca.com", Role.LIDER, Division.CORPORATIVO),
        ("gestor", "Gestor INOVAGAB", "gestor@aguiabranca.com", Role.GESTOR, Division.LOGISTICA),
        ("operador", "Operador INOVAGAB", "operador@aguiabranca.com", Role.OPERADOR, Division.LOGISTICA),
        // Operadores extras para o ranking mensal ter mais de uma pessoa.
        ("ana", "Ana Operadora", "ana@aguiabranca.com", Role.OPERADOR, Division.PASSAGEIROS),
        ("bruno", "Bruno Operador", "bruno@aguiabranca.com", Role.OPERADOR, Division.COMERCIO)
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;

        if (environment.IsProduction())
            logger.LogWarning("Seed habilitado em Production: as credenciais de demonstração são públicas. Desative Seed:Enabled fora de demos.");

        using var scope = scopes.CreateScope();
        await SeedAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var identity = services.GetRequiredService<IIdentityService>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var now = services.GetRequiredService<IClock>().UtcNow;

        var createdUsers = 0;
        foreach (var (_, name, email, role, division) in DemoUsers)
        {
            if (await db.Users.AsNoTracking().AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct)) continue;

            var created = await identity.CreateUserAsync(AppUser.Create(name, email, role, division, now.AddDays(-120)), DemoPassword, ct);
            if (created.IsFailure) throw new InvalidOperationException($"Seed: falha ao criar {email}: {created.FirstError.Message}");
            createdUsers++;
        }

        if (await db.Guidelines.AsNoTracking().AnyAsync(ct))
        {
            logger.LogInformation("Seed: dados de demonstração já existem; nada a fazer ({Created} usuários novos).", createdUsers);
            return;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(reports.Value.TimeZone);
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Carrega dentro da transação: em caso de retry o change tracker é limpo.
            var users = await db.Users.ToListAsync(token);
            var byEmail = DemoUsers.ToDictionary(d => d.Key, d => users.Single(u => u.Email == d.Email));
            new DemoDataBuilder(db, byEmail, now, zone).Build();
            return 0;
        }, ct);

        logger.LogInformation("Seed: dados de demonstração criados ({Created} usuários novos).", createdUsers);
    }

    /// <summary>Monta o conjunto de dados (ver "contrato" nos testes: 4 orientações, 6 ideias, 3 projetos).</summary>
    private sealed class DemoDataBuilder(AppDbContext db, Dictionary<string, AppUser> users, DateTime now, TimeZoneInfo zone)
    {
        private readonly List<PointEvent> _events = [];
        private readonly List<Idea> _ideas = [];

        public void Build()
        {
            var lider = users["lider"]; var gestor = users["gestor"];
            var operador = users["operador"]; var ana = users["ana"]; var bruno = users["bruno"];

            // ---------- orientações (+ histórico imutável) ----------
            var g1 = Guideline("Eficiência operacional na logística",
                "Reduzir tempo de ciclo e retrabalho nas operações de transporte e armazenagem.", Pillar.IDEIAS, "Campanha Eficiência 2026", lider, 60);
            var g2 = Guideline("Experiência do passageiro",
                "Tornar a jornada do passageiro mais simples, previsível e sem fricção.", Pillar.DIRECIONAMENTO, "Campanha Cliente 2026", lider, 45);
            var g3 = Guideline("Sustentabilidade e redução de custos",
                "Iniciativas que reduzam consumo de recursos e custo operacional com impacto ambiental positivo.", Pillar.PROJETOS, "Campanha Sustentabilidade 2026", lider, 30);
            var g4 = Guideline("Mensuração do retorno da inovação",
                "Padronizar indicadores para demonstrar o retorno real das iniciativas.", Pillar.MENSURACAO, null, lider, 20);

            // g2 foi criada e depois editada: o histórico guarda os dois momentos (o snapshot CREATED é anterior à edição).
            db.GuidelineHistory.Add(GuidelineHistoryEntry.From(g2, GuidelineAction.CREATED, lider.Id, lider.Name, g2.CreatedAt));
            g2.Update(g2.Title, g2.Description + " Prioridade: terminais rodoviários.", Pillar.DIRECIONAMENTO, "Campanha Cliente 2026", now.AddDays(-40));
            db.GuidelineHistory.Add(GuidelineHistoryEntry.From(g2, GuidelineAction.UPDATED, lider.Id, lider.Name, now.AddDays(-40)));

            // ---------- ideias e projetos ----------
            var i1 = Idea("Roteirização dinâmica de entregas com IA", "Usar dados de trânsito em tempo real para recalcular rotas.", "Tecnologia", operador, g1, TimeSpan.FromMinutes(-5));
            var i2 = Idea("Checklist digital de manutenção da frota", "Substituir o checklist em papel por formulário digital com fotos.", "Operações", operador, g1, TimeSpan.FromMinutes(-50));
            i2.SaveIce(new Ice(8, 7, 6), gestor.Id, now.AddMinutes(-30));

            var i3 = Idea("Check-in por QR Code nos terminais", "Permitir check-in do passageiro por QR Code, reduzindo filas.", "Atendimento ao cliente", ana, g2, TimeSpan.FromDays(-12));
            i3.SaveIce(new Ice(9, 8, 7), gestor.Id, now.AddMinutes(-90));
            Approve(i3, gestor, ana, now.AddMinutes(-40));

            var i4 = Idea("Painel de indicadores de pontualidade", "Painel único com pontualidade por linha e por terminal.", "Tecnologia", operador, g1, TimeSpan.FromDays(-90));
            i4.SaveIce(new Ice(9, 9, 8), gestor.Id, now.AddDays(-85));
            Approve(i4, gestor, operador, now.AddDays(-80));

            var i5 = Idea("Programa de indicação de clientes", "Bonificar clientes que indicarem novos contratos.", "Comercial e vendas", bruno, null, TimeSpan.FromDays(-25));
            i5.SaveIce(new Ice(4, 5, 5), gestor.Id, now.AddDays(-22));
            i5.Reject(gestor.Id, "Já existe iniciativa semelhante em andamento no comercial.", now.AddDays(-20));

            var i6 = Idea("Coleta seletiva integrada nos terminais", "Instalar pontos de coleta seletiva com destinação certificada.", "Sustentabilidade", bruno, g3, TimeSpan.FromDays(-40));
            i6.SaveIce(new Ice(7, 8, 6), gestor.Id, now.AddDays(-35));
            Approve(i6, gestor, bruno, now.AddHours(-2));

            // Projeto a partir de i3: rascunho + definição de escopo
            var p3 = Draft(i3, gestor, now.AddMinutes(-40));
            Update(p3, gestor, now.AddMinutes(-20), "Definição de escopo e orçamento",
                d => d with { Investment = 45_000m, StatusText = "Escopo em definição", TargetDate = now.AddDays(90) });

            // Projeto a partir de i4: ciclo completo até CONCLUIDO (ROI positivo)
            var p4 = Draft(i4, gestor, now.AddDays(-80));
            Update(p4, gestor, now.AddDays(-70), "Início da execução",
                d => d with { Stage = ProjectStage.EM_EXECUCAO, Investment = 120_000m, StatusText = "Em desenvolvimento", TargetDate = now.AddDays(-20) });
            Update(p4, gestor, now.AddDays(-40), "Primeiros ganhos medidos",
                d => d with { FinancialReturn = 90_000m, ProductivityGain = 6.5m });
            var completion = Update(p4, gestor, now.AddDays(-15), "Meta atingida",
                d => d with { Stage = ProjectStage.CONCLUIDO, StatusText = "Entregue", FinancialReturn = 310_000m, ProductivityGain = 12.5m, CostReduction = 45_000m });
            if (completion.BecameCompleted && i4.MarkImplemented(now.AddDays(-15)))
                Award(operador, PointsRules.IdeaImplemented, PointReason.IDEA_IMPLEMENTED, i4.Id, now.AddDays(-15));

            // Projeto a partir de i6: em execução, dentro do prazo
            var p6 = Draft(i6, gestor, now.AddHours(-2));
            Update(p6, gestor, now.AddHours(-1), "Piloto em 3 terminais",
                d => d with { Stage = ProjectStage.EM_EXECUCAO, Investment = 80_000m, StatusText = "Piloto em andamento",
                    TargetDate = now.AddDays(45), FinancialReturn = 25_000m, ProductivityGain = 4m, CostReduction = 8_000m });

            // ---------- badges (mesma regra do servidor) ----------
            foreach (var user in users.Values)
            {
                var earned = BadgeEvaluator.Evaluate(user, _ideas.Where(i => i.AuthorId == user.Id), zone);
                user.AddBadges(earned);
            }

            db.Guidelines.AddRange(g1, g2, g3, g4);
            foreach (var g in new[] { g1, g3, g4 })
                db.GuidelineHistory.Add(GuidelineHistoryEntry.From(g, GuidelineAction.CREATED, lider.Id, lider.Name, g.CreatedAt));
            db.Ideas.AddRange(_ideas);
            db.PointEvents.AddRange(_events);
        }

        // ---------- helpers ----------
        private Guideline Guideline(string title, string description, Pillar pillar, string? campaign, AppUser author, int daysAgo) =>
            Domain.Entities.Guideline.Create(title, description, pillar, campaign, author.Id, author.Name, now.AddDays(-daysAgo));

        private Idea Idea(string title, string description, string category, AppUser author, Guideline? guideline, TimeSpan offset)
        {
            var at = now + offset;
            var idea = Domain.Entities.Idea.Create(title, description, category, author.Division, guideline?.Id, author.Id, author.Name, at);
            _ideas.Add(idea);
            Award(author, idea.CreationPoints, PointReason.IDEA_CREATED, idea.Id, at);
            return idea;
        }

        private void Approve(Idea idea, AppUser reviewer, AppUser author, DateTime at)
        {
            idea.Approve(reviewer.Id, at);
            Award(author, PointsRules.IdeaApproved, PointReason.IDEA_APPROVED, idea.Id, at);
        }

        private Project Draft(Idea idea, AppUser manager, DateTime at)
        {
            var project = Project.CreateDraftFromIdea(idea, manager.Id, manager.Name, at);
            db.Projects.Add(project);
            db.ProjectUpdates.Add(ProjectUpdate.Create(project.Id, manager.Id, manager.Name,
                $"Criado automaticamente a partir da ideia: {idea.Title}", [], at));
            return project;
        }

        private ProjectUpdateOutcome Update(Project project, AppUser manager, DateTime at, string note, Func<ProjectData, ProjectData> change)
        {
            var current = new ProjectData(project.Title, project.Description, project.Stage, project.StatusText, project.Investment,
                project.TargetDate, project.FinancialReturn, project.ProductivityGain, project.CostReduction, project.Division,
                project.GuidelineId);
            var outcome = project.ApplyUpdate(change(current), at);
            db.ProjectUpdates.Add(ProjectUpdate.Create(project.Id, manager.Id, manager.Name, note, outcome.Changes, at));
            return outcome;
        }

        private void Award(AppUser user, int delta, PointReason reason, string refId, DateTime at)
        {
            var effective = user.ApplyPoints(delta);
            if (effective != 0) _events.Add(PointEvent.Create(user.Id, effective, reason, refId, at));
        }
    }
}
