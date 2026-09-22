using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Tests.Features.Ideas;

/// <summary>Cenário em memória: 1 operador, 1 gestor, 1 líder, uma orientação e os serviços reais de gamificação/resposta.</summary>
public abstract class IdeasTestBase
{
    protected readonly InMemoryUsers Users = new();
    protected readonly InMemoryGuidelines Guidelines = new();
    protected readonly InMemoryIdeas Ideas = new();
    protected readonly InMemoryProjects Projects = new();
    protected readonly InMemoryProjectUpdates ProjectUpdates = new();
    protected readonly InMemoryPointEvents Events = new();
    protected readonly FakeUnitOfWork Uow = new();
    protected readonly FakeClock Clock = new(new DateTime(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc));

    protected readonly AppUser Operador, Gestor, Lider;
    protected readonly Guideline Guideline;

    protected IdeasTestBase()
    {
        Operador = AddUser("Operador", Role.OPERADOR);
        Gestor = AddUser("Gestor", Role.GESTOR);
        Lider = AddUser("Líder", Role.LIDER);
        Guideline = Domain.Entities.Guideline.Create("Eficiência operacional", "d", Pillar.IDEIAS, null, Lider.Id, Lider.Name, Clock.UtcNow);
        Guidelines.Items.Add(Guideline);
    }

    private AppUser AddUser(string name, Role role)
    {
        var user = AppUser.Create(name, $"{Guid.NewGuid():N}@x.com", role, Division.LOGISTICA, Clock.UtcNow.AddDays(-30));
        Users.Items.Add(user);
        return user;
    }

    protected FakeCurrentUser As(AppUser user) => new()
    {
        Id = user.Id, Name = user.Name, Role = user.Role, Division = user.Division
    };

    protected GamificationService Gamification() => new(Events, Ideas, Uow, Clock, new FakeTimeZone());
    protected IdeaResponseFactory Responses() => new(Guidelines, Projects);

    protected Idea AddIdea(AppUser author, IdeaStatus status = IdeaStatus.SUBMETIDA, string? guidelineId = null,
        string title = "Ideia de teste", Division? division = null, Ice? ice = null, DateTime? createdAt = null)
    {
        var at = createdAt ?? Clock.UtcNow;
        var idea = Idea.Create(title, "descrição", "tecnologia", division ?? author.Division, guidelineId, author.Id, author.Name, at);
        if (ice is not null) idea.SaveIce(ice, Gestor.Id, at);
        switch (status)
        {
            case IdeaStatus.EM_ANALISE when ice is null: idea.SaveIce(new Ice(5, 5, 5), Gestor.Id, at); break;
            case IdeaStatus.APROVADA: idea.Approve(Gestor.Id, at); break;
            case IdeaStatus.IMPLEMENTADA: idea.Approve(Gestor.Id, at); idea.MarkImplemented(at); break;
            case IdeaStatus.REJEITADA: idea.Reject(Gestor.Id, "não agora", at); break;
        }
        Ideas.Items.Add(idea);
        return idea;
    }
}
