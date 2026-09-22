using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Guidelines.Get;

public sealed class GetGuidelineHandler(IGuidelineRepository guidelines) : IHandler<GetGuidelineQuery, GuidelineResponse>
{
    public async Task<Result<GuidelineResponse>> HandleAsync(GetGuidelineQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return GuidelineErrors.NotFound;

        var guideline = await guidelines.GetByIdAsync(request.Id, ct);
        return guideline is null ? GuidelineErrors.NotFound : GuidelineResponse.From(guideline);
    }
}
