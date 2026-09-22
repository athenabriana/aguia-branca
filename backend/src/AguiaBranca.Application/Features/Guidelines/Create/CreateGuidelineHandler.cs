using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Features.Guidelines.Create;

/// <summary>Cria a orientação e o registro histórico <c>CREATED</c> na mesma transação. Autor = usuário autenticado (nunca o corpo).</summary>
public sealed class CreateGuidelineHandler(
    IValidationService validation,
    IGuidelineRepository guidelines,
    IGuidelineHistoryRepository history,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<CreateGuidelineCommand, GuidelineResponse>
{
    public async Task<Result<GuidelineResponse>> HandleAsync(CreateGuidelineCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<GuidelineResponse>.Fail(errors);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<GuidelineResponse>>(async token =>
            {
                var now = clock.UtcNow;
                var guideline = Guideline.Create(
                    request.Title!, request.Description ?? string.Empty, request.Pillar!.Value, request.Campaign,
                    currentUser.Id, currentUser.Name, now);

                await guidelines.AddAsync(guideline, token);
                await history.AddAsync(
                    GuidelineHistoryEntry.From(guideline, GuidelineAction.CREATED, currentUser.Id, currentUser.Name, now), token);

                return GuidelineResponse.From(guideline);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<GuidelineResponse>();
        }
    }
}
