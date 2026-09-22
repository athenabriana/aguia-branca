using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Ideas.Create;

/// <summary>
/// Cadastra a ideia (status SUBMETIDA, autor = usuário autenticado) e, na mesma transação, credita +10 (+5 com orientação)
/// e concede as badges devidas (R2-03.1, R2-03.10, R2-05).
/// </summary>
public sealed class CreateIdeaHandler(
    IValidationService validation,
    IIdeaRepository ideas,
    IGuidelineRepository guidelines,
    IUserRepository users,
    GamificationService gamification,
    IdeaResponseFactory responses,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<CreateIdeaCommand, IdeaResponse>
{
    public async Task<Result<IdeaResponse>> HandleAsync(CreateIdeaCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<IdeaResponse>.Fail(errors);

        var guidelineId = string.IsNullOrWhiteSpace(request.GuidelineId) ? null : request.GuidelineId.Trim();

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<IdeaResponse>>(async token =>
            {
                if (guidelineId is not null && !await guidelines.ExistsAsync(guidelineId, token))
                    return IdeaErrors.GuidelineNotFound();

                var author = await users.GetByIdAsync(currentUser.Id, token);
                if (author is null) return IdeaErrors.UserNotFound;

                var idea = Idea.Create(
                    request.Title!, request.Description ?? string.Empty, request.Category!,
                    request.Division ?? author.Division, guidelineId, author.Id, author.Name, clock.UtcNow);
                await ideas.AddAsync(idea, token);

                var awarded = await gamification.AwardAsync(author, PointReason.IDEA_CREATED, idea.CreationPoints, idea.Id, token);
                await gamification.GrantEarnedBadgesAsync(author, token);

                return await responses.BuildAsync(idea, token, pointsAwarded: awarded);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<IdeaResponse>();
        }
    }
}
