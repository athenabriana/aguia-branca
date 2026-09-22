using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Features.Reports.Guidelines;

/// <summary>Retorno por estratégia: impacto de cada orientação (com projeto primeiro, ROI decrescente).</summary>
public sealed class ListGuidelineReportsHandler(
    IIdeaRepository ideas, IProjectRepository projects, IGuidelineRepository guidelines, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<ListGuidelineReportsQuery, GuidelineReportList>
{
    public async Task<Result<GuidelineReportList>> HandleAsync(ListGuidelineReportsQuery request, CancellationToken ct)
    {
        var summary = ReportCalculator.Compute(
            await ideas.ListAllAsync(request.Division, ct), await projects.ListAllAsync(request.Division, ct),
            await guidelines.ListAsync(ct), new ReportFilters(request.Period, request.Division), clock.UtcNow, timeZone.ReportTimeZone);

        return new GuidelineReportList(summary.Period, summary.Division, summary.GeneratedAt, summary.GuidelineImpacts);
    }
}
