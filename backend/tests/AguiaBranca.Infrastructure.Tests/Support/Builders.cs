using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Infrastructure.Tests.Support;

internal static class Builders
{
    public static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    public static AppUser User(Role role = Role.OPERADOR, string name = "Operador", string? email = null) =>
        AppUser.Create(name, email ?? $"{Guid.NewGuid():N}@aguiabranca.com", role, Division.LOGISTICA, Now);

    public static Guideline Guideline(string title = "Eficiência operacional", string? campaign = "2026", Pillar pillar = Pillar.IDEIAS) =>
        Domain.Entities.Guideline.Create(title, "Descrição", pillar, campaign, Domain.Common.EntityId.New(), "Líder", Now);

    public static Idea Idea(AppUser? author = null, string? guidelineId = null, DateTime? createdAt = null,
        Division division = Division.LOGISTICA, string title = "Roteirização com IA")
    {
        author ??= User();
        return Domain.Entities.Idea.Create(title, "Descrição", "tecnologia", division, guidelineId, author.Id, author.Name, createdAt ?? Now);
    }

    public static ProjectData ProjectData(
        ProjectStage stage = ProjectStage.PLANEJAMENTO, decimal investment = 100_000m, decimal ret = 0m,
        Division division = Division.LOGISTICA, string? guidelineId = null) =>
        new("Projeto X", "Desc", stage, "Em andamento", investment, null, ret, 12.5m, 3000.75m, division, guidelineId);

    public static Project Project(ProjectData? data = null, string? originatingIdeaId = null, DateTime? now = null) =>
        Domain.Entities.Project.Create(data ?? ProjectData(), Domain.Common.EntityId.New(), "Gestor", now ?? Now, originatingIdeaId);
}
