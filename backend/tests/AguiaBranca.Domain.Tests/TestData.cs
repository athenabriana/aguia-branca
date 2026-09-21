using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    public const string ReviewerId = "665f00000000000000000001";
    public const string ValidGuidelineId = "665f00000000000000000002";

    public static AppUser User(Role role = Role.OPERADOR) =>
        AppUser.Create("Operador", "operador@aguiabranca.com", role, Division.LOGISTICA, Now);

    public static Idea NewIdea(
        AppUser? author = null, string? guidelineId = null, DateTime? createdAt = null, string title = "Roteirização com IA") =>
        Idea.Create(title, "Descrição", "tecnologia", Division.LOGISTICA, guidelineId,
            (author ?? User()).Id, (author ?? User()).Name, createdAt ?? Now);

    /// <summary>Leva a ideia até o status desejado pelo caminho válido da máquina de estados.</summary>
    public static Idea IdeaWithStatus(
        IdeaStatus status, AppUser? author = null, string? guidelineId = null, DateTime? createdAt = null)
    {
        var idea = NewIdea(author, guidelineId, createdAt);
        switch (status)
        {
            case IdeaStatus.SUBMETIDA: break;
            case IdeaStatus.EM_ANALISE: idea.SaveIce(new Ice(5, 5, 5), ReviewerId, Now); break;
            case IdeaStatus.APROVADA: idea.Approve(ReviewerId, Now); break;
            case IdeaStatus.IMPLEMENTADA: idea.Approve(ReviewerId, Now); idea.MarkImplemented(Now); break;
            case IdeaStatus.REJEITADA: idea.Reject(ReviewerId, "não agora", Now); break;
        }
        return idea;
    }

    public static ProjectData ProjectData(
        ProjectStage stage = ProjectStage.PLANEJAMENTO, decimal investment = 100_000m, decimal financialReturn = 0m) =>
        new("Projeto X", "Desc", stage, "Em andamento", investment, null, financialReturn, 0m, 0m,
            Division.LOGISTICA, null);

    public static Project NewProject(ProjectData? data = null) =>
        Project.Create(data ?? ProjectData(), ReviewerId, "Gestor", Now);

    public static IReadOnlyList<string> AllBadges => Badges.All;
}
