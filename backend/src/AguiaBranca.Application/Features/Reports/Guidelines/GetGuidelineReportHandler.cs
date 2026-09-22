using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Reports.Guidelines;

/// <summary>Detalhe de uma orientação: ideias, projetos, investimento, retorno, lucro e ROI.</summary>
public sealed class GetGuidelineReportHandler(
    IIdeaRepository ideas, IProjectRepository projects, IGuidelineRepository guidelines, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<GetGuidelineReportQuery, GuidelineReportDetail>
{
    public async Task<Result<GuidelineReportDetail>> HandleAsync(GetGuidelineReportQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return ReportErrors.GuidelineNotFound;
        var guideline = await guidelines.GetByIdAsync(request.Id, ct);
        if (guideline is null) return ReportErrors.GuidelineNotFound;

        return ReportCalculator.ComputeGuideline(
            guideline, await ideas.ListAllAsync(request.Division, ct), await projects.ListAllAsync(request.Division, ct),
            new ReportFilters(request.Period, request.Division), clock.UtcNow, timeZone.ReportTimeZone);
    }
}
