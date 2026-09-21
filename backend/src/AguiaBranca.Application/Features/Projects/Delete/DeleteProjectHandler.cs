using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Projects.Delete;

/// <summary>Remove o projeto e o seu histórico. A ideia de origem permanece no status em que está (R2-04.7).</summary>
public sealed class DeleteProjectHandler(
    IProjectRepository projects, IProjectUpdateRepository projectUpdates, IUnitOfWork unitOfWork) : IHandler<DeleteProjectCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteProjectCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return ProjectErrors.NotFound;

        return await unitOfWork.ExecuteInTransactionAsync<Result<Unit>>(async token =>
        {
            var project = await projects.GetByIdAsync(request.Id, token);
            if (project is null) return ProjectErrors.NotFound;

            await projectUpdates.RemoveByProjectAsync(project.Id, token);
            projects.Remove(project);
            return Result.Ok();
        }, ct);
    }
}
