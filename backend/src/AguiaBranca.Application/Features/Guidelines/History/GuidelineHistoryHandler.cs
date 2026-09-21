using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;

namespace AguiaBranca.Application.Features.Guidelines.History;

/// <summary>Registro histórico das estratégias, com filtros por orientação, categoria, campanha e período.</summary>
public sealed class GuidelineHistoryHandler(IValidationService validation, IGuidelineHistoryRepository history)
    : IHandler<GuidelineHistoryQuery, PagedResult<GuidelineHistoryResponse>>
{
    public async Task<Result<PagedResult<GuidelineHistoryResponse>>> HandleAsync(GuidelineHistoryQuery request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<PagedResult<GuidelineHistoryResponse>>.Fail(errors);

        var campaign = string.IsNullOrWhiteSpace(request.Campaign) ? null : request.Campaign.Trim();
        var query = new Common.Abstractions.Repositories.GuidelineHistoryQuery(
            request.GuidelineId, request.Category, campaign, AsUtc(request.From), AsUtc(request.To));

        var page = await history.QueryAsync(query, new PageRequest(request.Page, request.PageSize), ct);
        return page.Map(GuidelineHistoryResponse.From);
    }

    /// <summary>Datas sem fuso vindas da query string ("2026-09-21") são tratadas como UTC.</summary>
    private static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v!.Value, DateTimeKind.Utc)
    };
}
