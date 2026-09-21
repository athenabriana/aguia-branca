using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Features.Ideas.Delete;

/// <summary>Exclusão pelo autor enquanto SUBMETIDA; estorna −10/−15 (clamp em 0) na mesma transação (R2-03.10).</summary>
public sealed class DeleteIdeaHandler(
    IIdeaRepository ideas,
    IUserRepository users,
    GamificationService gamification,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IHandler<DeleteIdeaCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteIdeaCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return IdeaErrors.NotFound;

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<Unit>>(async token =>
            {
                var idea = await ideas.GetByIdAsync(request.Id, token);
                if (idea is null) return IdeaErrors.NotFound;
                if (currentUser.DenyEdit(idea) is { } denied) return denied;

                idea.EnsureDeletable();

                var author = await users.GetByIdAsync(idea.AuthorId, token);
                if (author is not null)
                    await gamification.AwardAsync(author, PointReason.IDEA_DELETED, PointsRules.ForDeletion(idea.HasStrategicLink), idea.Id, token);

                ideas.Remove(idea);
                return Result.Ok();
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<Unit>();
        }
    }
}
