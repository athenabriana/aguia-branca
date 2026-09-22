using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Seed;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Seed;

/// <summary>Também documenta o "contrato" dos dados de demonstração usados pelos demais testes e pelo app.</summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SeedTests(MongoFixture fixture) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await fixture.CreateDatabaseAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ServiceProvider Provider(bool seedEnabled = true, string environment = "Development")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Mongo"] = fixture.ConnectionString,
            ["Mongo:Database"] = _db.Name,
            ["Jwt:Key"] = new string('k', 48),
            ["Seed:Enabled"] = seedEnabled.ToString()
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment(environment));
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private static async Task RunSeederAsync(ServiceProvider provider) =>
        await provider.GetServices<IHostedService>().OfType<DatabaseSeeder>().Single().StartAsync(default);

    private async Task<long> Count(string collection) => await _db.Raw(collection).CountDocumentsAsync(new BsonDocument());

    private async Task<T[]> All<T>(Func<AppDbContext, IQueryable<T>> query) where T : class
    {
        await using var ctx = _db.CreateContext();
        return await query(ctx).AsNoTracking().ToArrayAsync();
    }

    [Fact]
    public async Task Seed_CreatesTheDocumentedDataset()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        (await Count(Collections.Users)).Should().Be(5);
        (await Count(Collections.Guidelines)).Should().Be(4);
        (await Count(Collections.GuidelineHistory)).Should().Be(5, "4 criações + 1 edição");
        (await Count(Collections.Ideas)).Should().Be(6);
        (await Count(Collections.Projects)).Should().Be(3);
        (await Count(Collections.ProjectUpdates)).Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task Seed_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);
        var before = new[] { Collections.Users, Collections.Guidelines, Collections.Ideas, Collections.Projects, Collections.ProjectUpdates, Collections.PointEvents }
            .ToDictionary(c => c, c => Count(c).Result);

        await RunSeederAsync(provider);
        await RunSeederAsync(provider);

        foreach (var (collection, expected) in before)
            (await Count(collection)).Should().Be(expected, collection);
    }

    [Fact]
    public async Task Seed_Disabled_DoesNothing()
    {
        await using var provider = Provider(seedEnabled: false);
        await RunSeederAsync(provider);

        (await Count(Collections.Users)).Should().Be(0);
        (await Count(Collections.Guidelines)).Should().Be(0);
    }

    [Fact]
    public async Task Seed_IsResumable_WhenOnlyTheUsersExist()
    {
        await using var provider = Provider();
        using (var scope = provider.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            foreach (var (_, name, email, role, division) in DatabaseSeeder.DemoUsers.Take(2))
                (await identity.CreateUserAsync(AppUser.Create(name, email, role, division, DateTime.UtcNow), DatabaseSeeder.DemoPassword, default)).IsSuccess.Should().BeTrue();
        }

        await RunSeederAsync(provider);

        (await Count(Collections.Users)).Should().Be(5);
        (await Count(Collections.Ideas)).Should().Be(6);
    }

    [Fact]
    public async Task Seed_DemoUsers_CanLogIn_WithTheDocumentedCredentials()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        foreach (var (_, _, email, role, division) in DatabaseSeeder.DemoUsers)
        {
            using var scope = provider.CreateScope();
            var check = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
                .CheckCredentialsAsync(email, "aguiabranca123", default);
            check.Status.Should().Be(CredentialStatus.Success, email);
            (check.User!.Role, check.User.Division).Should().Be((role, division));
        }
    }

    [Fact]
    public async Task Seed_IdeasCoverEveryStatus_WithConsistentData()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        var ideas = await All(c => c.Ideas);

        ideas.GroupBy(i => i.Status).ToDictionary(g => g.Key, g => g.Count()).Should().BeEquivalentTo(new Dictionary<IdeaStatus, int>
        {
            [IdeaStatus.SUBMETIDA] = 1, [IdeaStatus.EM_ANALISE] = 1, [IdeaStatus.APROVADA] = 2,
            [IdeaStatus.REJEITADA] = 1, [IdeaStatus.IMPLEMENTADA] = 1
        });
        ideas.Where(i => i.Status == IdeaStatus.EM_ANALISE).Should().OnlyContain(i => i.Ice != null);
        ideas.Single(i => i.Status == IdeaStatus.REJEITADA).ReviewComment.Should().NotBeNullOrWhiteSpace();
        ideas.Count(i => i.GuidelineId == null).Should().Be(1, "uma ideia sem orientação");
        ideas.Should().OnlyContain(i => i.CreatedAt <= i.UpdatedAt);
    }

    [Fact]
    public async Task Seed_PointsAndEvents_AreConsistent_AndBadgesFollowTheRules()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        var users = await All(c => c.Users);
        var events = await All(c => c.PointEvents);

        foreach (var user in users)
            user.Points.Should().Be(events.Where(e => e.UserId == user.Id).Sum(e => e.Delta), $"razão de pontos de {user.Name}");

        var byName = users.ToDictionary(u => u.Name);
        byName["Operador INOVAGAB"].Points.Should().Be(15 + 15 + (15 + 50 + 200));
        byName["Operador INOVAGAB"].Badges.Should().BeEquivalentTo([Badges.PrimeiraIdeia, Badges.Estrategista, Badges.ImpactoReal]);
        byName["Ana Operadora"].Points.Should().Be(15 + 50);
        byName["Ana Operadora"].Badges.Should().BeEquivalentTo([Badges.PrimeiraIdeia, Badges.Estrategista]);
        byName["Bruno Operador"].Points.Should().Be(10 + (15 + 50));
        byName["Bruno Operador"].Badges.Should().BeEquivalentTo([Badges.PrimeiraIdeia, Badges.Estrategista]);
        byName["Líder INOVAGAB"].Points.Should().Be(0);
        byName["Gestor INOVAGAB"].Badges.Should().BeEmpty();
    }

    [Fact]
    public async Task Seed_Projects_AreLinkedToApprovedIdeas_WithHistoryAndDiffs()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        var projects = await All(c => c.Projects);
        var ideas = (await All(c => c.Ideas)).ToDictionary(i => i.Id);
        var updates = await All(c => c.ProjectUpdates);

        projects.Should().HaveCount(3);
        projects.Select(p => p.Stage).Should().BeEquivalentTo([ProjectStage.PLANEJAMENTO, ProjectStage.EM_EXECUCAO, ProjectStage.CONCLUIDO]);
        projects.Should().OnlyContain(p => p.OriginatingIdeaId != null && ideas[p.OriginatingIdeaId!].IsApprovedOrLater);
        projects.Should().OnlyContain(p => p.Title.StartsWith("PROJ: ") && p.GuidelineId == ideas[p.OriginatingIdeaId!].GuidelineId);

        foreach (var project in projects)
        {
            var history = updates.Where(u => u.ProjectId == project.Id).OrderBy(u => u.CreatedAt).ToList();
            history.Count.Should().BeGreaterThanOrEqualTo(2);
            history[0].Note.Should().StartWith("Criado automaticamente a partir da ideia:");
            project.Version.Should().Be(history.Count, "versão = 1 + número de edições");
        }

        var done = projects.Single(p => p.Stage == ProjectStage.CONCLUIDO);
        done.FinancialReturn.Should().BeGreaterThan(done.Investment, "ROI positivo (alimenta o funil)");
        var fields = updates.Where(u => u.ProjectId == done.Id).SelectMany(u => u.Changes).Select(c => c.Field).ToList();
        fields.Should().Contain(["stage", "investment", "financialReturn"]);
        projects.Single(p => p.Stage == ProjectStage.EM_EXECUCAO).TargetDate.Should().BeAfter(DateTime.UtcNow, "dentro do prazo");
        ideas[done.OriginatingIdeaId!].Status.Should().Be(IdeaStatus.IMPLEMENTADA);
    }

    [Fact]
    public async Task Seed_GuidelineHistory_HasCreationAndAnEdit_ForAllGuidelines()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        var guidelines = await All(c => c.Guidelines);
        var history = await All(c => c.GuidelineHistory);

        guidelines.Select(g => g.Pillar).Should().BeEquivalentTo(Enum.GetValues<Pillar>());
        guidelines.Count(g => g.Campaign != null).Should().Be(3);
        history.Where(h => h.Action == GuidelineAction.CREATED).Select(h => h.GuidelineId).Should().BeEquivalentTo(guidelines.Select(g => g.Id));
        var edited = history.Single(h => h.Action == GuidelineAction.UPDATED);
        history.Single(h => h.GuidelineId == edited.GuidelineId && h.Action == GuidelineAction.CREATED)
            .Snapshot.Description.Should().NotBe(edited.Snapshot.Description, "o snapshot CREATED guarda o texto original");
    }

    [Fact]
    public async Task Seed_RankingMonth_HasMoreThanOneOperator()
    {
        await using var provider = Provider();
        await RunSeederAsync(provider);

        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var events = await All(c => c.PointEvents.Where(e => e.CreatedAt >= start));

        events.Select(e => e.UserId).Distinct().Count().Should().BeGreaterThanOrEqualTo(2);
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
