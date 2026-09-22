using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Guidelines.Delete;

/// <summary>
/// Exclusão definitiva (R-02.5): o histórico <c>DELETED</c> guarda o último estado e <b>sobrevive</b> à orientação.
/// Ideias/projetos vinculados não são tocados (o vínculo passa a ser "órfão", R2-02.6).
/// </summary>
public sealed class DeleteGuidelineHandler(
    IGuidelineRepository guidelines,
    IGuidelineHistoryRepository history,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<DeleteGuidelineCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteGuidelineCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return GuidelineErrors.NotFound;

        return await unitOfWork.ExecuteInTransactionAsync<Result<Unit>>(async token =>
        {
            var guideline = await guidelines.GetByIdAsync(request.Id, token);
            if (guideline is null) return GuidelineErrors.NotFound;

            await history.AddAsync(
                GuidelineHistoryEntry.From(guideline, GuidelineAction.DELETED, currentUser.Id, currentUser.Name, clock.UtcNow), token);
            guidelines.Remove(guideline);
            return Result.Ok();
        }, ct);
    }
}
