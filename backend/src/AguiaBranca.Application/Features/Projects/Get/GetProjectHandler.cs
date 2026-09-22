using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Projects.Get;

public sealed class GetProjectHandler(IProjectRepository projects, ProjectResponseFactory responses) : IHandler<GetProjectQuery, ProjectResponse>
{
    public async Task<Result<ProjectResponse>> HandleAsync(GetProjectQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return ProjectErrors.NotFound;
        var project = await projects.GetByIdAsync(request.Id, ct);
        return project is null ? ProjectErrors.NotFound : await responses.BuildAsync(project, ct);
    }
}
