using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Features.Ideas.SaveIce;

/// <summary>Salva a matriz ICE (score calculado no servidor); SUBMETIDA passa a EM_ANALISE automaticamente (R-03.12).</summary>
public sealed class SaveIceHandler(
    IValidationService validation,
    IIdeaRepository ideas,
    IdeaResponseFactory responses,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<SaveIceCommand, IdeaResponse>
{
    public async Task<Result<IdeaResponse>> HandleAsync(SaveIceCommand request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return IdeaErrors.NotFound;

        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<IdeaResponse>.Fail(errors);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync<Result<IdeaResponse>>(async token =>
            {
                var idea = await ideas.GetByIdAsync(request.Id, token);
                if (idea is null) return IdeaErrors.NotFound;

                idea.SaveIce(new Ice(request.Impact!.Value, request.Confidence!.Value, request.Ease!.Value), currentUser.Id, clock.UtcNow);
                return await responses.BuildAsync(idea, token);
            }, ct);
        }
        catch (DomainException ex)
        {
            return ex.ToResult<IdeaResponse>();
        }
    }
}
