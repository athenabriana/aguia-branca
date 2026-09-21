using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;

namespace AguiaBranca.Application.Features.Projects.List;

/// <summary>Projetos do mais recentemente alterado para o mais antigo, com filtros por estágio, divisão e orientação.</summary>
public sealed class ListProjectsHandler(IValidationService validation, IProjectRepository projects, ProjectResponseFactory responses)
    : IHandler<ListProjectsQuery, PagedResult<ProjectResponse>>
{
    public async Task<Result<PagedResult<ProjectResponse>>> HandleAsync(ListProjectsQuery request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<PagedResult<ProjectResponse>>.Fail(errors);

        var guidelineId = string.IsNullOrWhiteSpace(request.GuidelineId) ? null : request.GuidelineId.Trim();
        var page = await projects.QueryAsync(new ProjectQuery(request.Stage, request.Division, guidelineId), new PageRequest(request.Page, request.PageSize), ct);
        var built = await responses.BuildAsync(page.Items.ToList(), ct);
        return new PagedResult<ProjectResponse>(built, page.Page, page.PageSize, page.TotalItems);
    }
}
