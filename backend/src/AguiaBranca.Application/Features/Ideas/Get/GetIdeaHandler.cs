using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Ideas.Get;

public sealed class GetIdeaHandler(IIdeaRepository ideas, IdeaResponseFactory responses, ICurrentUser currentUser)
    : IHandler<GetIdeaQuery, IdeaResponse>
{
    public async Task<Result<IdeaResponse>> HandleAsync(GetIdeaQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return IdeaErrors.NotFound;

        var idea = await ideas.GetByIdAsync(request.Id, ct);
        // Ideia alheia é 404 para o operador (não revela que existe).
        if (idea is null || !currentUser.CanSee(idea)) return IdeaErrors.NotFound;

        return await responses.BuildAsync(idea, ct);
    }
}
