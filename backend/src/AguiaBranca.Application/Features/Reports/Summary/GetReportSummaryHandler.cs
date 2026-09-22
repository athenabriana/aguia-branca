using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Features.Reports.Summary;

/// <summary>Resumo do dashboard (funil, KPIs, sparkline, impacto por orientação, projetos por ROI). Somente leitura.</summary>
public sealed class GetReportSummaryHandler(
    IIdeaRepository ideas, IProjectRepository projects, IGuidelineRepository guidelines, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<GetReportSummaryQuery, ReportSummary>
{
    public async Task<Result<ReportSummary>> HandleAsync(GetReportSummaryQuery request, CancellationToken ct)
    {
        var ideaList = await ideas.ListAllAsync(request.Division, ct);
        var projectList = await projects.ListAllAsync(request.Division, ct);
        var guidelineList = await guidelines.ListAsync(ct);

        return ReportCalculator.Compute(
            ideaList, projectList, guidelineList, new ReportFilters(request.Period, request.Division), clock.UtcNow, timeZone.ReportTimeZone);
    }
}
