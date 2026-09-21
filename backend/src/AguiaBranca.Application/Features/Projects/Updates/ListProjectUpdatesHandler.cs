using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Projects.Updates;

/// <summary>Histórico do projeto (timeline), mais recente primeiro (R-04.5).</summary>
public sealed class ListProjectUpdatesHandler(IValidationService validation, IProjectRepository projects, IProjectUpdateRepository updates)
    : IHandler<ListProjectUpdatesQuery, PagedResult<ProjectUpdateResponse>>
{
    public async Task<Result<PagedResult<ProjectUpdateResponse>>> HandleAsync(ListProjectUpdatesQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.ProjectId)) return ProjectErrors.NotFound;

        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<PagedResult<ProjectUpdateResponse>>.Fail(errors);

        if (await projects.GetByIdAsync(request.ProjectId, ct) is null) return ProjectErrors.NotFound;

        var page = await updates.ListByProjectAsync(request.ProjectId, new PageRequest(request.Page, request.PageSize), ct);
        return page.Map(ProjectUpdateResponse.From);
    }
}
