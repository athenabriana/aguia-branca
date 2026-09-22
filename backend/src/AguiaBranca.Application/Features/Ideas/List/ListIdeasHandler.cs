using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Ideas.List;

/// <summary>
/// Escopos (R2-03.3/.4): <c>mine</c> = as próprias; <c>curation</c> = SUBMETIDA + EM_ANALISE por ICE desc (sem ICE ao fim);
/// <c>all</c> = todas. Operador é sempre restrito às próprias, qualquer que seja o escopo pedido.
/// </summary>
public sealed class ListIdeasHandler(
    IValidationService validation, IIdeaRepository ideas, IdeaResponseFactory responses, ICurrentUser currentUser)
    : IHandler<ListIdeasQuery, PagedResult<IdeaResponse>>
{
    private static readonly IdeaStatus[] CurationStatuses = [IdeaStatus.SUBMETIDA, IdeaStatus.EM_ANALISE];

    public async Task<Result<PagedResult<IdeaResponse>>> HandleAsync(ListIdeasQuery request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<PagedResult<IdeaResponse>>.Fail(errors);

        var isOperator = currentUser.Role == Role.OPERADOR;
        var scope = request.Scope ?? (isOperator ? IdeaScope.MINE : IdeaScope.ALL);

        IReadOnlyCollection<IdeaStatus>? statuses = null;
        if (scope == IdeaScope.CURATION)
            statuses = request.Status is { } s ? CurationStatuses.Where(c => c == s).ToArray() : CurationStatuses;
        else if (request.Status is { } only)
            statuses = [only];

        var guidelineId = string.IsNullOrWhiteSpace(request.GuidelineId) ? null : request.GuidelineId.Trim();
        var query = new IdeaQuery(
            AuthorId: isOperator || scope == IdeaScope.MINE ? currentUser.Id : null,
            Statuses: statuses,
            GuidelineId: guidelineId,
            Division: request.Division,
            Sort: scope == IdeaScope.CURATION ? IdeaSort.ICE_SCORE_DESC : IdeaSort.CREATED_DESC);

        // Escopo "curation" pedindo um status fora dele (ex.: APROVADA) não tem resultado possível.
        var page = new PageRequest(request.Page, request.PageSize);
        if (statuses is { Count: 0 }) return PagedResult<IdeaResponse>.Empty(page);

        var found = await ideas.QueryAsync(query, page, ct);
        var built = await responses.BuildAsync(found.Items.ToList(), ct);
        return new PagedResult<IdeaResponse>(built, found.Page, found.PageSize, found.TotalItems);
    }
}
