using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Features.Ideas.Approve;

/// <summary>
/// Automação 1 (R-03.8 / R2-03.8), numa única transação: ideia → APROVADA, projeto rascunho herdando os dados da ideia,
/// primeira entrada do histórico do projeto, +50 pontos ao autor e avaliação de badges.
/// <para>
/// <b>Idempotente</b>: reaprovar devolve o mesmo projeto sem duplicar projeto nem pontos — inclusive sob concorrência
/// (o índice único parcial de <c>originatingIdeaId</c> e o conflito de escrita do Mongo decidem a corrida).
/// O autor nunca aprova a própria ideia.
/// </para>
/// </summary>
public sealed class ApproveIdeaHandler(
    IIdeaRepository ideas,
    IProjectRepository projects,
    IProjectUpdateRepository projectUpdates,
    IUserRepository users,
    GamificationService gamification,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<ApproveIdeaCommand, ApproveIdeaResult>
{
    public async Task<Result<ApproveIdeaResult>> HandleAsync(ApproveIdeaCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return IdeaErrors.NotFound;

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<ApproveIdeaResult>>(async token =>
            {
                var idea = await ideas.GetByIdAsync(request.Id, token);
                if (idea is null) return IdeaErrors.NotFound;

                var now = clock.UtcNow;
                // Lança SELF_APPROVAL_FORBIDDEN (autor) ou IDEA_INVALID_STATE (rejeitada); false = já aprovada.
                if (!idea.Approve(currentUser.Id, now))
                {
                    var existing = await projects.GetByOriginatingIdeaIdAsync(idea.Id, token);
                    return new ApproveIdeaResult(idea.Id, existing?.Id, AlreadyApproved: true);
                }

                var project = Project.CreateDraftFromIdea(idea, currentUser.Id, currentUser.Name, now);
                await projects.AddAsync(project, token);
                await projectUpdates.AddAsync(ProjectUpdate.Create(
                    project.Id, currentUser.Id, currentUser.Name,
                    $"Criado automaticamente a partir da ideia: {idea.Title}", [], now), token);

                var author = await users.GetByIdAsync(idea.AuthorId, token);
                if (author is not null)
                {
                    await gamification.AwardAsync(author, PointReason.IDEA_APPROVED, PointsRules.IdeaApproved, idea.Id, token);
                    await gamification.GrantEarnedBadgesAsync(author, token);
                }

                return new ApproveIdeaResult(idea.Id, project.Id, AlreadyApproved: false);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<ApproveIdeaResult>();
        }
        catch (DuplicateKeyException)
        {
            // Outra requisição aprovou a mesma ideia primeiro (índice único de originatingIdeaId): a nossa é a "repetida".
            var existing = await projects.GetByOriginatingIdeaIdAsync(request.Id, ct);
            return new ApproveIdeaResult(request.Id, existing?.Id, AlreadyApproved: true);
        }
    }
}
