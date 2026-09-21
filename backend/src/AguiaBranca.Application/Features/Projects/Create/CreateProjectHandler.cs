using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Projects.Create;

/// <summary>Cadastro direto de projeto pelo gestor (R-04.8). Grava a primeira entrada do histórico: "Projeto criado".</summary>
public sealed class CreateProjectHandler(
    IValidationService validation,
    IGuidelineRepository guidelines,
    IUserRepository users,
    IProjectRepository projects,
    IProjectUpdateRepository projectUpdates,
    ProjectResponseFactory responses,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<CreateProjectCommand, ProjectResponse>
{
    public const string CreationNote = "Projeto criado";

    public async Task<Result<ProjectResponse>> HandleAsync(CreateProjectCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<ProjectResponse>.Fail(errors);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<ProjectResponse>>(async token =>
            {
                var resolved = await new ProjectInputResolver(guidelines, users).ResolveAsync(request.GuidelineId, request.ResponsibleId, token);
                if (resolved.Error is not null) return resolved.Error;

                var now = clock.UtcNow;
                var data = ProjectInputResolver.ToData(
                    request.Title, request.Description, request.Stage, request.StatusText, request.Investment, request.TargetDate,
                    request.FinancialReturn, request.ProductivityGain, request.CostReduction, request.Division!.Value, resolved);

                var project = Project.Create(data, currentUser.Id, currentUser.Name, now);
                await projects.AddAsync(project, token);
                await projectUpdates.AddAsync(ProjectUpdate.Create(project.Id, currentUser.Id, currentUser.Name, CreationNote, [], now), token);

                return await responses.BuildAsync(project, token);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<ProjectResponse>();
        }
    }
}
