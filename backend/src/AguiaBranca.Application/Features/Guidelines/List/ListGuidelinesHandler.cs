using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;

namespace AguiaBranca.Application.Features.Guidelines.List;

/// <summary>Orientações vigentes, da mais recentemente alterada para a mais antiga (R2-02.3).</summary>
public sealed class ListGuidelinesHandler(IValidationService validation, IGuidelineRepository guidelines)
    : IHandler<ListGuidelinesQuery, PagedResult<GuidelineResponse>>
{
    public async Task<Result<PagedResult<GuidelineResponse>>> HandleAsync(ListGuidelinesQuery request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<PagedResult<GuidelineResponse>>.Fail(errors);

        var page = await guidelines.ListPagedAsync(new PageRequest(request.Page, request.PageSize), ct);
        return page.Map(GuidelineResponse.From);
    }
}
