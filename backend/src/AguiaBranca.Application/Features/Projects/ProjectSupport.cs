using AguiaBranca.Application.Common;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Features.Projects;

/// <summary>Monta as respostas de projeto com o título da orientação (nulo se órfã).</summary>
public sealed class ProjectResponseFactory(IGuidelineRepository guidelines)
{
    public async Task<IReadOnlyList<ProjectResponse>> BuildAsync(IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        if (projects.Count == 0) return [];
        var titles = await guidelines.GetTitlesAsync(projects.Where(p => p.GuidelineId is not null).Select(p => p.GuidelineId!), ct);
        return projects.Select(p => ProjectResponse.From(p, p.GuidelineId is not null && titles.TryGetValue(p.GuidelineId, out var t) ? t : null)).ToList();
    }

    public async Task<ProjectResponse> BuildAsync(Project project, CancellationToken ct) => (await BuildAsync([project], ct))[0];
}

/// <summary>Valida vínculos externos de um projeto (orientação e responsável) e monta os dados de domínio.</summary>
internal sealed class ProjectInputResolver(IGuidelineRepository guidelines, IUserRepository users)
{
    public sealed record Resolved(Error? Error, string? GuidelineId, string? ResponsibleId, string? ResponsibleName);

    public async Task<Resolved> ResolveAsync(string? guidelineId, string? responsibleId, CancellationToken ct)
    {
        var gid = string.IsNullOrWhiteSpace(guidelineId) ? null : guidelineId.Trim();
        if (gid is not null && !await guidelines.ExistsAsync(gid, ct))
            return new Resolved(ProjectErrors.GuidelineNotFound(), null, null, null);

        var rid = string.IsNullOrWhiteSpace(responsibleId) ? null : responsibleId.Trim();
        string? responsibleName = null;
        if (rid is not null)
        {
            var user = await users.GetByIdAsync(rid, ct);
            if (user is null || user.Role == Role.OPERADOR)
                return new Resolved(ProjectErrors.ResponsibleNotFound(), null, null, null);
            responsibleName = user.Name;
        }

        return new Resolved(null, gid, rid, responsibleName);
    }

    public static ProjectData ToData(
        string? title, string? description, ProjectStage? stage, string? statusText, decimal? investment, DateTime? targetDate,
        decimal? financialReturn, decimal? productivityGain, decimal? costReduction, Division division, Resolved resolved) =>
        new(title ?? string.Empty, description ?? string.Empty, stage ?? ProjectStage.PLANEJAMENTO, statusText ?? string.Empty,
            investment ?? 0m, targetDate.AsUtc(), financialReturn ?? 0m, productivityGain ?? 0m, costReduction ?? 0m,
            division, resolved.GuidelineId, resolved.ResponsibleId, resolved.ResponsibleName);
}

/// <summary>
/// Automação 2 (R-03.13 / R2-04.5): quando o projeto é concluído, a ideia de origem vira IMPLEMENTADA, o autor recebe +200
/// e as badges são reavaliadas. Idempotente por estado: só credita quando a ideia realmente muda de APROVADA para IMPLEMENTADA.
/// </summary>
public sealed class ProjectCompletionAutomation(
    IIdeaRepository ideas, IUserRepository users, GamificationService gamification, IClock clock)
{
    /// <summary>Devolve <c>true</c> se a ideia foi marcada como implementada agora.</summary>
    public async Task<bool> ApplyAsync(Project project, CancellationToken ct)
    {
        if (project.OriginatingIdeaId is null) return false;

        var idea = await ideas.GetByIdAsync(project.OriginatingIdeaId, ct);
        if (idea is null || !idea.MarkImplemented(clock.UtcNow)) return false;

        var author = await users.GetByIdAsync(idea.AuthorId, ct);
        if (author is not null)
        {
            await gamification.AwardAsync(author, PointReason.IDEA_IMPLEMENTED, PointsRules.IdeaImplemented, idea.Id, ct);
            await gamification.GrantEarnedBadgesAsync(author, ct);
        }
        return true;
    }
}
