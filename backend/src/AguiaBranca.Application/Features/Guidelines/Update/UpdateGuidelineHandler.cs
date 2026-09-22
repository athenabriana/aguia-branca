using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Guidelines.Update;

/// <summary>Edita a orientação e grava a entrada <c>UPDATED</c> (com o snapshot já editado) na mesma transação.</summary>
public sealed class UpdateGuidelineHandler(
    IValidationService validation,
    IGuidelineRepository guidelines,
    IGuidelineHistoryRepository history,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<UpdateGuidelineCommand, GuidelineResponse>
{
    public async Task<Result<GuidelineResponse>> HandleAsync(UpdateGuidelineCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return GuidelineErrors.NotFound;

        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<GuidelineResponse>.Fail(errors);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<GuidelineResponse>>(async token =>
            {
                var guideline = await guidelines.GetByIdAsync(request.Id, token);
                if (guideline is null) return GuidelineErrors.NotFound;

                var now = clock.UtcNow;
                guideline.Update(request.Title!, request.Description ?? string.Empty, request.Pillar!.Value, request.Campaign, now);
                await history.AddAsync(
                    GuidelineHistoryEntry.From(guideline, GuidelineAction.UPDATED, currentUser.Id, currentUser.Name, now), token);

                return GuidelineResponse.From(guideline);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<GuidelineResponse>();
        }
    }
}
