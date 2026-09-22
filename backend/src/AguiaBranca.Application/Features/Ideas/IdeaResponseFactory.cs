using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Application.Features.Ideas;

/// <summary>Monta as respostas de ideia com o título da orientação (nulo se órfã) e o projeto vinculado.</summary>
public sealed class IdeaResponseFactory(IGuidelineRepository guidelines, IProjectRepository projects)
{
    public async Task<IReadOnlyList<IdeaResponse>> BuildAsync(IReadOnlyCollection<Idea> ideas, CancellationToken ct)
    {
        if (ideas.Count == 0) return [];

        var titles = await guidelines.GetTitlesAsync(ideas.Where(i => i.GuidelineId is not null).Select(i => i.GuidelineId!), ct);
        var linked = (await projects.GetByOriginatingIdeaIdsAsync(ideas.Select(i => i.Id), ct))
            .ToDictionary(p => p.OriginatingIdeaId!, p => new LinkedProjectResponse(p.Id, p.Stage, p.UpdatedAt));

        return ideas.Select(i => IdeaResponse.From(
            i,
            i.GuidelineId is not null && titles.TryGetValue(i.GuidelineId, out var title) ? title : null,
            linked.GetValueOrDefault(i.Id))).ToList();
    }

    public async Task<IdeaResponse> BuildAsync(Idea idea, CancellationToken ct, int? pointsAwarded = null)
    {
        var built = (await BuildAsync([idea], ct))[0];
        return pointsAwarded is null ? built : built with { PointsAwarded = pointsAwarded };
    }
}
