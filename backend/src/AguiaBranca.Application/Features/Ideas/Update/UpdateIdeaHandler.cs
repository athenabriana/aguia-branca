using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Ideas.Update;

/// <summary>Edição pelo autor, somente enquanto SUBMETIDA (R-03.11 / R2-03.5).</summary>
public sealed class UpdateIdeaHandler(
    IValidationService validation,
    IIdeaRepository ideas,
    IGuidelineRepository guidelines,
    IdeaResponseFactory responses,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<UpdateIdeaCommand, IdeaResponse>
{
    public async Task<Result<IdeaResponse>> HandleAsync(UpdateIdeaCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return IdeaErrors.NotFound;

        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<IdeaResponse>.Fail(errors);

        var guidelineId = string.IsNullOrWhiteSpace(request.GuidelineId) ? null : request.GuidelineId.Trim();

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<IdeaResponse>>(async token =>
            {
                var idea = await ideas.GetByIdAsync(request.Id, token);
                if (idea is null) return IdeaErrors.NotFound;
                if (currentUser.DenyEdit(idea) is { } denied) return denied;

                if (guidelineId is not null && !await guidelines.ExistsAsync(guidelineId, token))
                    return IdeaErrors.GuidelineNotFound();

                idea.EditContent(
                    request.Title!, request.Description ?? string.Empty, request.Category!,
                    request.Division ?? idea.Division, guidelineId, clock.UtcNow);

                return await responses.BuildAsync(idea, token);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<IdeaResponse>();
        }
    }
}
