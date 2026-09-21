using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Projects.Update;

/// <summary>
/// Edição pelo gestor (qualquer gestor edita qualquer projeto). Numa única transação: aplica a mudança, grava a entrada de
/// histórico com o <b>diff calculado no servidor</b> e, se o projeto acabou de ser concluído, executa a automação da
/// conclusão (ideia de origem → IMPLEMENTADA, +200 ao autor, badges).
/// </summary>
public sealed class UpdateProjectHandler(
    IValidationService validation,
    IGuidelineRepository guidelines,
    IUserRepository users,
    IProjectRepository projects,
    IProjectUpdateRepository projectUpdates,
    ProjectCompletionAutomation completion,
    ProjectResponseFactory responses,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<UpdateProjectCommand, ProjectResponse>
{
    public async Task<Result<ProjectResponse>> HandleAsync(UpdateProjectCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return ProjectErrors.NotFound;

        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<ProjectResponse>.Fail(errors);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<ProjectResponse>>(async token =>
            {
                var project = await projects.GetByIdAsync(request.Id, token);
                if (project is null) return ProjectErrors.NotFound;

                var resolved = await new ProjectInputResolver(guidelines, users).ResolveAsync(request.GuidelineId, request.ResponsibleId, token);
                if (resolved.Error is not null) return resolved.Error;

                var now = clock.UtcNow;
                var data = ProjectInputResolver.ToData(
                    request.Title, request.Description, request.Stage, request.StatusText, request.Investment, request.TargetDate,
                    request.FinancialReturn, request.ProductivityGain, request.CostReduction, request.Division!.Value, resolved);

                var outcome = project.ApplyUpdate(data, now, request.Version);
                await projectUpdates.AddAsync(ProjectUpdate.Create(
                    project.Id, currentUser.Id, currentUser.Name, request.Note, outcome.Changes, now), token);

                if (outcome.BecameCompleted)
                    await completion.ApplyAsync(project, token);

                return await responses.BuildAsync(project, token);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<ProjectResponse>();
        }
        catch (ConcurrencyConflictException ex)
        {
            return ProjectErrors.Concurrency(ex.Message);
        }
    }
}
